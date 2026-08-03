// repoPicker.js - progressive enhancement for folder selection and drag/drop
// Provides showDirectoryPicker-based detection when available, falling back to file input (webkitdirectory) and drag/drop.

window.repoPicker = {
    // Detect repository metadata using showDirectoryPicker when available.
    detectFromDirectoryPicker: async function () {
        if (!window.showDirectoryPicker) return null;
        try {
            const dirHandle = await window.showDirectoryPicker();
            console.log('repoPicker.detectFromDirectoryPicker: selected dirHandle.name=', dirHandle.name);
            const summary = { name: dirHandle.name || '', detectedType: 'None', detectedProjectCount: 0 };
            // Walk directory entries (non-recursive first pass) then try recursion for .csproj files
            const csprojFiles = [];
            for await (const [name, handle] of dirHandle) {
                if (handle.kind === 'file') {
                    if (name.endsWith('.sln') || name.endsWith('.slnx')) summary.detectedType = 'DotNet';
                    if (name.endsWith('.pom.xml')) summary.detectedType = 'JavaMaven';
                    if (name.endsWith('build.gradle') || name.endsWith('settings.gradle')) summary.detectedType = summary.detectedType === 'DotNet' ? 'DotNet' : 'JavaGradle';
                    if (name.endsWith('.csproj')) csprojFiles.push(name);
                } else if (handle.kind === 'directory') {
                    // recursive search for csproj
                    try {
                        for await (const entry of handle.values()) {
                            if (entry.kind === 'file' && entry.name.endsWith('.csproj')) csprojFiles.push(entry.name);
                        }
                    } catch (e) { }
                }
            }
            if (csprojFiles.length > 0) summary.detectedType = 'DotNet';
            summary.detectedProjectCount = csprojFiles.length;
            console.log('repoPicker.detectFromDirectoryPicker: summary=', summary);
            return summary;
        } catch (e) {
            console.log('repoPicker.detectFromDirectoryPicker: error', e);
            return null;
        }
    },

    // Trigger hidden file input
    triggerFileInput: function (inputId) {
        const el = document.getElementById(inputId);
        if (el) el.click();
    },

    // Process a file input (webkitdirectory)
    detectFromFileInput: function (input) {
        const files = input.files;
        if (!files || files.length === 0) return null;
        let detectedType = 'None';
        let csprojCount = 0;
        // Try to obtain an absolute path when the browser exposes it (Playwright/Chromium may set `path` on File)
        let rootName = '';
        try {
            if (files[0] && files[0].path) {
                // files[0].path is a full path to the file; use its directory as the selected folder
                const fullPath = files[0].path;
                const lastSep = Math.max(fullPath.lastIndexOf('/'), fullPath.lastIndexOf('\\'));
                if (lastSep > 0) {
                    rootName = fullPath.substring(0, lastSep);
                } else {
                    rootName = fullPath;
                }
            }
        } catch (e) { }
        if (!rootName) {
            const rootParts = (files[0].webkitRelativePath || files[0].name).split('/');
            rootName = rootParts[0] || '';
        }
        const sampleFiles = [];
        for (let i = 0; i < files.length; i++) {
            const f = files[i];
            const name = f.name.toLowerCase();
            const rel = f.webkitRelativePath || f.name;
            sampleFiles.push(rel);
            if (name.endsWith('.sln') || name.endsWith('.slnx')) detectedType = 'DotNet';
            if (rel.endsWith('pom.xml')) detectedType = 'JavaMaven';
            if (rel.endsWith('build.gradle') || rel.endsWith('settings.gradle')) detectedType = detectedType === 'DotNet' ? 'DotNet' : 'JavaGradle';
            if (name.endsWith('.csproj')) csprojCount++;
        }
        if (csprojCount > 0) detectedType = 'DotNet';
        const result = { name: rootName, detectedType: detectedType, detectedProjectCount: csprojCount, sampleFiles: sampleFiles };
        console.log('repoPicker.detectFromFileInput: result=', result);
        // Also log first few files for visibility
        try { console.log('repoPicker.detectFromFileInput: files[0]=', files[0], 'webkitRelativePath=', files[0].webkitRelativePath, 'path=', files[0].path); } catch (e) { }
        return result;
    },

    // Attach drag/drop to elementId. dotNetRef should implement a 'clientDetected' method.
    attachDropHandler: function (elementId, dotNetRef) {
        const el = document.getElementById(elementId);
        if (!el) return;
        el.addEventListener('dragover', (ev) => {
            ev.preventDefault();
            el.classList.add('drag-over');
            // Provide visual feedback while dragging
            el.style.borderStyle = 'dashed';
            el.style.backgroundColor = '#f8f9fa';
        });
        el.addEventListener('dragleave', (ev) => {
            el.classList.remove('drag-over');
            el.style.borderStyle = '';
            el.style.backgroundColor = '';
        });
        el.addEventListener('drop', async (ev) => {
            ev.preventDefault();
            el.classList.remove('drag-over');
            el.style.borderStyle = '';
            el.style.backgroundColor = '';
            const dt = ev.dataTransfer;
            if (!dt) return;
            // If FileSystemHandle is available on items, try to use it
            if (dt.items && dt.items.length > 0 && dt.items[0].getAsFileSystemHandle) {
                try {
                    const handle = await dt.items[0].getAsFileSystemHandle();
                    // If it's a directory handle, walk it
                    if (handle && handle.kind === 'directory') {
                        // Reuse showDirectoryPicker-like detection
                        try {
                            const summary = { name: handle.name || '', detectedType: 'None', detectedProjectCount: 0 };
                            const csprojFiles = [];
                            for await (const [name, h] of handle) {
                                if (h.kind === 'file') {
                                    const lname = name.toLowerCase();
                                    if (lname.endsWith('.sln') || lname.endsWith('.slnx')) summary.detectedType = 'DotNet';
                                    if (lname.endsWith('.pom.xml')) summary.detectedType = 'JavaMaven';
                                    if (lname.endsWith('build.gradle') || lname.endsWith('settings.gradle')) summary.detectedType = summary.detectedType === 'DotNet' ? 'DotNet' : 'JavaGradle';
                                    if (lname.endsWith('.csproj')) csprojFiles.push(name);
                                }
                            }
                            if (csprojFiles.length > 0) summary.detectedType = 'DotNet';
                            summary.detectedProjectCount = csprojFiles.length;
                            await dotNetRef.invokeMethodAsync('OnClientSelectionDetected', summary);
                            return;
                        } catch (e) { }
                    }
                } catch (e) { }
            }
            // Fallback: use files list
            if (dt.files && dt.files.length > 0) {
                // Create a fake input-like list by copying files into a DataTransfer and using webkitRelativePath where available
                const files = dt.files;
                // Try to infer root name
                let rootName = '';
                if (files[0] && files[0].webkitRelativePath) {
                    rootName = files[0].webkitRelativePath.split('/')[0];
                }
                let detectedType = 'None';
                let csprojCount = 0;
                for (let i = 0; i < files.length; i++) {
                    const f = files[i];
                    const name = f.name.toLowerCase();
                    const rel = f.webkitRelativePath || f.name;
                    if (name.endsWith('.sln') || name.endsWith('.slnx')) detectedType = 'DotNet';
                    if (rel.endsWith('pom.xml')) detectedType = 'JavaMaven';
                    if (rel.endsWith('build.gradle') || rel.endsWith('settings.gradle')) detectedType = detectedType === 'DotNet' ? 'DotNet' : 'JavaGradle';
                    if (name.endsWith('.csproj')) csprojCount++;
                }
                if (csprojCount > 0) detectedType = 'DotNet';
                await dotNetRef.invokeMethodAsync('OnClientSelectionDetected', { name: rootName, detectedType: detectedType, detectedProjectCount: csprojCount });
            }
        });
    },

    // Attach a change handler to a hidden folder input so programmatic file selection (e.g., Playwright SetInputFiles)
    // triggers client-side detection and notifies the .NET runtime.
    attachFolderInputHandler: function (inputId, dotNetRef) {
        const el = document.getElementById(inputId);
        if (!el) return;
        let lastDispatchedKey = '';
        const notifyIfFilesPresent = async () => {
            if (!el || !el.files || el.files.length === 0) return false;
            const first = el.files[0];
            const key = `${el.files.length}|${first ? first.name : ''}|${first ? (first.webkitRelativePath || '') : ''}`;
            if (key === lastDispatchedKey) return true;
            const summary = window.repoPicker.detectFromFileInput(el);
            if (summary) {
                lastDispatchedKey = key;
                await dotNetRef.invokeMethodAsync('OnClientSelectionDetected', summary);
                return true;
            }
            return false;
        };
        el.addEventListener('change', async () => {
            try {
                await notifyIfFilesPresent();
            } catch (e) { }
        });

        // Some automation drivers (e.g., Playwright SetInputFiles) may set files without firing a change event.
        // Poll briefly after wiring up the handler to detect programmatic assignments.
        try {
            let checks = 0;
            const maxChecks = 25; // ~5 seconds at 200ms interval
            const iv = setInterval(async () => {
                try {
                    const handled = await notifyIfFilesPresent();
                    checks++;
                    if (handled || checks >= maxChecks) {
                        clearInterval(iv);
                    }
                } catch (e) {
                    clearInterval(iv);
                }
            }, 200);
        } catch (e) { }
    }
};
