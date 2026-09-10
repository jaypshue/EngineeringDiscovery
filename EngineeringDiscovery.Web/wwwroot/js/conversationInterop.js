const handlers = new WeakMap();
const maxImageBytes = 4 * 1024 * 1024;

export function attachPasteHandler(element, dotNetRef) {
    if (!element) return;

    detachPasteHandler(element);

    const handler = async (event) => {
        const items = Array.from(event.clipboardData?.items ?? []);
        const imageItem = items.find(item => item.kind === "file" && item.type.startsWith("image/"));
        if (!imageItem) return;

        const file = imageItem.getAsFile();
        if (!file || file.size > maxImageBytes) return;

        event.preventDefault();

        const dataUrl = await readAsDataUrl(file);
        await dotNetRef.invokeMethodAsync(
            "HandlePastedImage",
            dataUrl,
            file.type,
            file.name || "pasted-image",
            file.size);
    };

    element.addEventListener("paste", handler);
    handlers.set(element, handler);
}

export function detachPasteHandler(element) {
    if (!element) return;

    const handler = handlers.get(element);
    if (handler) {
        element.removeEventListener("paste", handler);
        handlers.delete(element);
    }
}

function readAsDataUrl(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(file);
    });
}

export function isNearBottom(element, threshold = 64) {
    if (!element) return true;

    return element.scrollHeight - element.scrollTop - element.clientHeight <= threshold;
}

export function scrollToBottom(element) {
    if (!element) return;

    requestAnimationFrame(() => {
        element.scrollTop = element.scrollHeight;
    });
}
