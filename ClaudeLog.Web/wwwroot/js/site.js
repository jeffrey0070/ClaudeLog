// ClaudeLog - Client-side JavaScript

let currentPage = 1;
const pageSize = 200;
let currentSearch = '';
let selectedEntryId = null;
let allEntries = [];

// Debounce function for search
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Load entries from API
async function loadEntries(search = '', page = 1, append = false) {
    try {
        const url = `/api/entries?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`;
        const response = await fetch(url);
        const entries = await response.json();

        if (!append) {
            allEntries = entries;
            renderEntriesList(entries);
        } else {
            allEntries = [...allEntries, ...entries];
            renderEntriesList(allEntries);
        }

        // Show/hide load more button
        document.getElementById('loadMore').style.display =
            entries.length === pageSize ? 'block' : 'none';

        currentPage = page;
        currentSearch = search;
    } catch (error) {
        console.error('Error loading entries:', error);
        logError('UI', 'Failed to load entries', error.toString());
    }
}

// Render entries list grouped by section
function renderEntriesList(entries) {
    const container = document.getElementById('entriesList');

    if (entries.length === 0) {
        container.innerHTML = '<div class="text-center text-muted p-4">No conversations found</div>';
        return;
    }

    // Group by section
    const sections = {};
    entries.forEach(entry => {
        if (!sections[entry.sectionId]) {
            sections[entry.sectionId] = {
                tool: entry.tool,
                createdAt: entry.sectionCreatedAt,
                entries: []
            };
        }
        sections[entry.sectionId].entries.push(entry);
    });

    // Sort sections by date desc
    const sortedSections = Object.entries(sections).sort((a, b) =>
        new Date(b[1].createdAt) - new Date(a[1].createdAt)
    );

    let html = '';
    sortedSections.forEach(([sectionId, section]) => {
        const sectionDate = new Date(section.createdAt).toLocaleDateString();
        const sectionTime = new Date(section.createdAt).toLocaleTimeString();
        html += `
            <div class="section-group mb-3">
                <div class="section-header p-2 bg-light fw-bold text-muted small">
                    ${sectionDate} ${sectionTime} - ${section.tool}
                </div>
                <div class="entries-in-section">
        `;

        section.entries.forEach(entry => {
            const entryDate = new Date(entry.createdAt).toLocaleDateString();
            const entryTime = new Date(entry.createdAt).toLocaleTimeString();
            const entryDateTime = `${entryDate} ${entryTime}`;
            const isSelected = entry.id === selectedEntryId;
            html += `
                <div class="entry-item p-2 border-bottom ${isSelected ? 'selected' : ''}"
                     onclick="selectEntry(${entry.id})"
                     data-entry-id="${entry.id}"
                     title="${entryDateTime}">
                    <div class="entry-title">${escapeHtml(entry.title)}</div>
                </div>
            `;
        });

        html += `
                </div>
            </div>
        `;
    });

    container.innerHTML = html;
}

// Select and load entry detail
async function selectEntry(id) {
    selectedEntryId = id;

    // Update selection highlight
    document.querySelectorAll('.entry-item').forEach(item => {
        item.classList.remove('selected');
    });
    document.querySelector(`[data-entry-id="${id}"]`)?.classList.add('selected');

    try {
        const response = await fetch(`/api/entries/${id}`);
        const entry = await response.json();
        renderEntryDetail(entry);
    } catch (error) {
        console.error('Error loading entry detail:', error);
        logError('UI', 'Failed to load entry detail', error.toString());
    }
}

// Render entry detail with markdown
function renderEntryDetail(entry) {
    const detailView = document.getElementById('detailView');
    const timestamp = new Date(entry.createdAt).toLocaleString();

    detailView.innerHTML = `
        <div class="entry-detail">
            <div class="mb-3">
                <h4 id="entryTitle" class="editable-title" onclick="editTitle(${entry.id}, this)">
                    ${escapeHtml(entry.title)}
                </h4>
                <div class="small text-muted">
                    ${timestamp} | Session: ${entry.sectionId} | ${entry.tool}
                </div>
            </div>

            <div class="mb-4">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <h5>Question</h5>
                    <button class="btn btn-sm btn-outline-secondary" onclick="copyToClipboard('question')">
                        Copy
                    </button>
                </div>
                <div id="question" class="p-3 bg-light border rounded">
                    ${escapeHtml(entry.question)}
                </div>
            </div>

            <div class="mb-4">
                <div class="d-flex justify-content-between align-items-center mb-2">
                    <h5>Response</h5>
                    <div>
                        <button class="btn btn-sm btn-outline-secondary" onclick="copyToClipboard('response')">
                            Copy
                        </button>
                        <button class="btn btn-sm btn-outline-secondary" onclick="copyBoth()">
                            Copy Both
                        </button>
                    </div>
                </div>
                <div id="response" class="p-3 bg-light border rounded markdown-content" data-raw="${escapeHtml(entry.response)}">
                    ${renderMarkdown(entry.response)}
                </div>
            </div>
        </div>
    `;
}

// Render markdown (client-side fallback)
function renderMarkdown(text) {
    // Simple markdown rendering - in production you'd use a library
    return escapeHtml(text)
        .replace(/```([^`]+)```/g, '<pre><code>$1</code></pre>')
        .replace(/`([^`]+)`/g, '<code>$1</code>')
        .replace(/\n/g, '<br>');
}

// Edit title inline
function editTitle(id, element) {
    const currentTitle = element.textContent.trim();
    const input = document.createElement('input');
    input.type = 'text';
    input.value = currentTitle;
    input.className = 'form-control';
    input.onblur = () => saveTitle(id, input.value, element);
    input.onkeypress = (e) => {
        if (e.key === 'Enter') {
            input.blur();
        }
    };

    element.innerHTML = '';
    element.appendChild(input);
    input.focus();
}

// Save edited title
async function saveTitle(id, newTitle, element) {
    try {
        const response = await fetch(`/api/entries/${id}/title`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ title: newTitle })
        });

        if (response.ok) {
            element.textContent = newTitle;
            // Update in list
            const listItem = document.querySelector(`[data-entry-id="${id}"] .entry-title`);
            if (listItem) {
                listItem.textContent = newTitle;
            }
        }
    } catch (error) {
        console.error('Error saving title:', error);
        logError('UI', 'Failed to save title', error.toString());
    }
}

// Copy to clipboard
async function copyToClipboard(elementId) {
    const element = document.getElementById(elementId);
    const text = element.dataset.raw || element.textContent;

    try {
        await navigator.clipboard.writeText(text);
        showToast('Copied to clipboard!');
    } catch (error) {
        console.error('Copy failed:', error);
    }
}

// Copy both question and response
async function copyBoth() {
    const question = document.getElementById('question').textContent;
    const response = document.getElementById('response').dataset.raw ||
                     document.getElementById('response').textContent;
    const combined = `Q: ${question}\n\nA: ${response}`;

    try {
        await navigator.clipboard.writeText(combined);
        showToast('Copied both to clipboard!');
    } catch (error) {
        console.error('Copy failed:', error);
    }
}

// Show toast notification
function showToast(message) {
    // Simple toast - in production you'd use Bootstrap toast or similar
    const toast = document.createElement('div');
    toast.className = 'position-fixed top-0 end-0 m-3 p-3 bg-success text-white rounded';
    toast.textContent = message;
    document.body.appendChild(toast);
    setTimeout(() => toast.remove(), 2000);
}

// Load more entries
function loadMoreEntries() {
    loadEntries(currentSearch, currentPage + 1, true);
}

// Log error to API
async function logError(source, message, detail) {
    try {
        await fetch('/api/errors', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ source, message, detail })
        });
    } catch (err) {
        console.error('Failed to log error:', err);
    }
}

// Escape HTML
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

// Initialize on page load
document.addEventListener('DOMContentLoaded', () => {
    loadEntries();

    // Search with debounce
    const searchInput = document.getElementById('searchInput');
    searchInput.addEventListener('input', debounce(() => {
        loadEntries(searchInput.value);
    }, 300));

    // Clear search
    document.getElementById('clearSearch').addEventListener('click', () => {
        searchInput.value = '';
        loadEntries('');
    });
});
