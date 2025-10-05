# Instructions for Claude

## 📖 Read This First

When you start a new session in this project, **READ `CONTEXT.md` FIRST** to understand the project state.

## 🔄 Update the App

When the user asks to **"update the app"**, perform these steps:

1. **Stop the running app**
   ```bash
   taskkill //F //IM ClaudeLog.Web.exe
   ```

2. **Rebuild the project**
   ```bash
   cd ClaudeLog.Web && dotnet clean && dotnet build
   ```

3. **Publish to deployment folder**
   ```bash
   cd ClaudeLog.Web && dotnet publish -c Release -o "C:/Apps/ClaudeLog.Web"
   ```

   *Note: If publish fails due to locked files, kill the process by PID and retry.*

4. **Start the app**
   ```bash
   cd "C:/Apps/ClaudeLog.Web" && start ClaudeLog.Web.exe
   ```

**Deployment Location:** `C:\Apps\ClaudeLog.Web`

## 📝 Documentation Preferences

This project follows a **single source of truth** principle to avoid duplication and conflicts:

1. **claude.md** (this file) - Instructions for Claude
2. **CONTEXT.md** - Current project state, what's done, what's next, critical bugs fixed
3. **README.md** - Complete setup and usage guide for users
4. **PROJECT_PLAN.md** - Original requirements and future roadmap

### Rules

- ❌ **DO NOT duplicate** content between files
- ❌ **DO NOT create** additional markdown files for documentation
- ✅ **DO reference** other files when needed (e.g., "See PROJECT_PLAN.md for...")
- ✅ **DO update** CONTEXT.md when significant work is completed
- ✅ **DO keep** documentation concise and up-to-date

### Single Source of Truth

- **Current state** → CONTEXT.md
- **Setup/usage** → README.md
- **Requirements/roadmap** → PROJECT_PLAN.md

Keep it simple. Keep it DRY (Don't Repeat Yourself).
