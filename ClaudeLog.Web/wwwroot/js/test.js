// API Testing Page JavaScript

let lastCreatedSectionId = null;
let lastCreatedEntryId = null;

// Display response in the response area
function displayResponse(title, status, data) {
    const display = document.getElementById('responseDisplay');
    const timestamp = new Date().toLocaleTimeString();

    const statusClass = status >= 200 && status < 300 ? 'success' : 'danger';

    let output = `[${timestamp}] ${title}\n`;
    output += `Status: ${status}\n`;
    output += `\n`;
    output += typeof data === 'string' ? data : JSON.stringify(data, null, 2);

    display.textContent = output;
    display.style.backgroundColor = status >= 200 && status < 300 ? '#d4edda' : '#f8d7da';
}

function clearResponse() {
    document.getElementById('responseDisplay').textContent = 'Response cleared...';
    document.getElementById('responseDisplay').style.backgroundColor = '#f8f9fa';
}

// API Helper
async function callApi(method, url, body = null) {
    try {
        const options = {
            method: method,
            headers: {
                'Content-Type': 'application/json'
            }
        };

        if (body) {
            options.body = JSON.stringify(body);
        }

        const response = await fetch(url, options);
        const data = await response.json();

        return { status: response.status, data };
    } catch (error) {
        return { status: 0, data: { error: error.message } };
    }
}

// Sections API
document.getElementById('createSectionForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const body = {
        tool: document.getElementById('sectionTool').value,
        sectionId: document.getElementById('sectionId').value || null
    };

    const result = await callApi('POST', '/api/sections', body);
    displayResponse('POST /api/sections', result.status, result.data);

    if (result.status === 200 && result.data.sectionId) {
        lastCreatedSectionId = result.data.sectionId;
        document.getElementById('entrySectionId').value = lastCreatedSectionId;
    }
});

document.getElementById('getSectionsForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const days = document.getElementById('sectionDays').value;
    const page = document.getElementById('sectionPage').value;
    const pageSize = document.getElementById('sectionPageSize').value;

    const url = `/api/sections?days=${days}&page=${page}&pageSize=${pageSize}`;
    const result = await callApi('GET', url);
    displayResponse('GET /api/sections', result.status, result.data);
});

// Entries API
document.getElementById('createEntryForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const body = {
        sectionId: document.getElementById('entrySectionId').value,
        question: document.getElementById('entryQuestion').value,
        response: document.getElementById('entryResponse').value
    };

    const result = await callApi('POST', '/api/entries', body);
    displayResponse('POST /api/entries', result.status, result.data);

    if (result.status === 200 && result.data.id) {
        lastCreatedEntryId = result.data.id;
        document.getElementById('entryGetId').value = lastCreatedEntryId;
        document.getElementById('entryUpdateId').value = lastCreatedEntryId;
    }
});

document.getElementById('getEntriesForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const search = document.getElementById('entrySearch').value;
    const page = document.getElementById('entryPage').value;
    const pageSize = document.getElementById('entryPageSize').value;

    const url = `/api/entries?search=${encodeURIComponent(search)}&page=${page}&pageSize=${pageSize}`;
    const result = await callApi('GET', url);
    displayResponse('GET /api/entries', result.status, result.data);
});

document.getElementById('getEntryByIdForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const id = document.getElementById('entryGetId').value;
    const url = `/api/entries/${id}`;
    const result = await callApi('GET', url);
    displayResponse(`GET /api/entries/${id}`, result.status, result.data);
});

document.getElementById('updateTitleForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const id = document.getElementById('entryUpdateId').value;
    const body = {
        title: document.getElementById('entryNewTitle').value
    };

    const url = `/api/entries/${id}/title`;
    const result = await callApi('PATCH', url, body);
    displayResponse(`PATCH /api/entries/${id}/title`, result.status, result.data);
});

// Errors API
document.getElementById('logErrorForm').addEventListener('submit', async (e) => {
    e.preventDefault();

    const body = {
        source: document.getElementById('errorSource').value,
        message: document.getElementById('errorMessage').value,
        detail: document.getElementById('errorDetail').value || null,
        path: document.getElementById('errorPath').value || null
    };

    const result = await callApi('POST', '/api/errors', body);
    displayResponse('POST /api/errors', result.status, result.data);
});

// Quick Actions
async function createTestData() {
    displayResponse('Creating test data...', 0, 'Please wait...');

    // Create section
    const sectionBody = {
        tool: 'TestTool',
        sectionId: null
    };

    const sectionResult = await callApi('POST', '/api/sections', sectionBody);

    if (sectionResult.status !== 200) {
        displayResponse('Failed to create test section', sectionResult.status, sectionResult.data);
        return;
    }

    const sectionId = sectionResult.data.sectionId;

    // Create entry
    const entryBody = {
        sectionId: sectionId,
        question: 'This is a test question created from the API test page',
        response: 'This is a test response. The API is working correctly!\n\nYou can now:\n- View this entry in the main UI\n- Search for it\n- Edit the title\n- Copy the content'
    };

    const entryResult = await callApi('POST', '/api/entries', entryBody);

    if (entryResult.status === 200) {
        lastCreatedSectionId = sectionId;
        lastCreatedEntryId = entryResult.data.id;

        document.getElementById('entrySectionId').value = sectionId;
        document.getElementById('entryGetId').value = entryResult.data.id;
        document.getElementById('entryUpdateId').value = entryResult.data.id;

        displayResponse('Test Data Created Successfully!', 200, {
            section: { sectionId: sectionId },
            entry: { id: entryResult.data.id },
            message: 'Visit the home page to see your test data!',
            nextSteps: [
                'Go to home page (click "Back to Home" button)',
                'You should see the test entry in the list',
                'Click it to view the full Q&A',
                'Try editing the title or copying the content'
            ]
        });
    } else {
        displayResponse('Failed to create test entry', entryResult.status, entryResult.data);
    }
}

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    displayResponse('API Testing Page Ready', 200, {
        message: 'Ready to test APIs',
        tip: 'Use the forms on the left to test different endpoints',
        quickStart: 'Click "Create Test Section + Entry" to quickly create sample data'
    });
});
