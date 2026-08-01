let cyInstance = null;

async function ensureCytoscape() {
    if (window.cytoscape) return;
    safeLog('loading cytoscape and plugins from CDN');
    // Load cytoscape from CDN
    await new Promise((res, rej) => {
        const s = document.createElement('script');
        s.src = 'https://unpkg.com/cytoscape@3.23.0/dist/cytoscape.min.js';
        s.onload = res; s.onerror = rej; document.head.appendChild(s);
    });
    // Load dagre for layout
    await new Promise((res, rej) => {
        const s = document.createElement('script');
        s.src = 'https://unpkg.com/dagre@0.8.5/dist/dagre.min.js';
        s.onload = res; s.onerror = rej; document.head.appendChild(s);
    });
    await new Promise((res, rej) => {
        const s = document.createElement('script');
        s.src = 'https://unpkg.com/cytoscape-dagre@2.3.2/cytoscape-dagre.js';
        s.onload = res; s.onerror = rej; document.head.appendChild(s);
    });
}

function safeLog(msg) {
    try {
        console.log('[graphInterop] ' + msg);        
    } catch { }
}

function safeError(msg, err) {
    try { console.error('[graphInterop] ' + msg, err); } catch { }
}

function buildStyle() {
    return [
        { selector: 'node', style: { 'label': 'data(label)', 'text-valign': 'center', 'color': '#dff3ff', 'text-outline-width': 0, 'background-color': '#2b6cb0', 'width': 'label', 'height': '36', 'padding': '6px' } },
        { selector: 'node[kind="Interface"]', style: { 'shape': 'roundrectangle', 'background-color': 'transparent', 'border-style': 'dashed', 'border-color': '#60a5fa', 'border-width': 1.5 } },
        { selector: 'node[kind="Enum"]', style: { 'shape': 'hexagon', 'background-color': '#f472b6' } },
        { selector: 'edge', style: { 'width': 1.2, 'line-color': '#9aa6b3', 'target-arrow-shape': 'triangle', 'target-arrow-color': '#9aa6b3', 'curve-style': 'bezier' } },
        { selector: 'edge[type="Implementation"]', style: { 'line-style': 'dashed', 'line-color': '#9fc3ff', 'target-arrow-color': '#9fc3ff' } },
        { selector: 'edge[type="Inheritance"]', style: { 'line-style': 'solid', 'line-color': '#7dd3fc', 'target-arrow-color': '#7dd3fc', 'width': 2 } },
        { selector: '.faded', style: { 'opacity': 0.12, 'text-opacity': 0 } },
        { selector: '.highlighted', style: { 'z-index': 9999, 'overlay-padding': '6px', 'transition-property': 'opacity', 'transition-duration': '200ms' } },
    ];
}

export async function init(containerId, elements, dotNetRef, viewState) {
    try {
        safeLog('initialization started');
        await ensureCytoscape();
    } catch (ex) {
        safeError('initialization error while loading cytoscape', ex);
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnGraphInitError', 'Failed to load cytoscape scripts: ' + (ex && ex.message ? ex.message : ex));
        return;
    }

    try {
        if (cyInstance) cyInstance.destroy();
        const container = document.getElementById(containerId);

        if (!container) {
            safeLog('container element not found: ' + containerId);
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnGraphInitError', 'Container element not found: ' + containerId);
            return;
        }

        const cw = container.clientWidth || 0;
        const ch = container.clientHeight || 0;
        safeLog('container found: id=' + containerId + ' size=' + cw + 'x' + ch);

        let nodeCount = 0, edgeCount = 0;
        try {
            if (Array.isArray(elements)) {
                elements.forEach(e => { try { if (e && e.data && e.data.source) edgeCount++; else nodeCount++; } catch { } });
            }
        } catch { }
        safeLog('data received: nodes=' + nodeCount + ' edges=' + edgeCount);

        const graphContainer = container.parentElement;

        console.log("graphContainer", graphContainer);
        console.log("graphContainer client", graphContainer.clientWidth, graphContainer.clientHeight);
        console.log("graphContainer rect", graphContainer.getBoundingClientRect());

        console.log("cy", container);
        console.log("cy client", container.clientWidth, container.clientHeight);
        console.log("cy rect", container.getBoundingClientRect());

        cyInstance = window.cytoscape({
            container: container,
            elements: elements,
            style: buildStyle(),        
            wheelSensitivity: 0.2,
            motionBlur: true,
        });

        safeLog('cytoscape instance created: nodes=' + cyInstance.nodes().length + ' edges=' + cyInstance.edges().length);

        const layout = cyInstance.layout({
            name: "cose",
            animate: true
        });

        layout.one("layoutstop", () => {
            safeLog('layout finished');
            cyInstance.fit(undefined, 40);
            cyInstance.center();
            safeLog('initial fit/center completed');

            const canvas = cyInstance.container().querySelector("canvas");
            safeLog(
                `container=${container.clientWidth}x${container.clientHeight} ` +
                `canvas=${canvas ? canvas.width + "x" + canvas.height : "none"}`
);

            safeLog(
                `viewport: zoom=${cyInstance.zoom()} ` +
                `pan=${JSON.stringify(cyInstance.pan())}`
            );
        });

        safeLog('layout started');
        layout.run();

        if (dotNetRef) {
            try { dotNetRef.invokeMethodAsync('OnGraphInitialized', cyInstance.nodes().length, cyInstance.edges().length, cw, ch); } catch { }
        }

        cyInstance.on('tap', 'node', function (evt) {
            const node = evt.target;
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnNodeSelected', node.id());
            spotlight(node.id());
        });

        cyInstance.on('dblclick', 'node', function (evt) {
            const node = evt.target; centerOn(node.id());
        });

        // maintain view state
        cyInstance.on('pan zoom', () => {
            if (!cyInstance) return;
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnViewStateChanged', getViewState());
        });

        // restore view state if provided
        if (viewState) setViewState(viewState);
    } catch (ex) {
        safeError('initialization error', ex);
        if (dotNetRef) dotNetRef.invokeMethodAsync('OnGraphInitError', ex && ex.message ? ex.message : String(ex));
    }
}

export function update(elements) {
    if (!cyInstance) return;
    cyInstance.startBatch();
    cyInstance.elements().remove();
    cyInstance.add(elements);
    try { cyInstance.layout({ name: 'cose', animate: true, fit: false }).run(); } catch { }
    cyInstance.endBatch();
}

export function fit() { if (cyInstance) cyInstance.fit(); }
export function reset() { if (cyInstance) { cyInstance.elements().removeClass('faded'); cyInstance.elements().removeClass('highlighted'); cyInstance.fit(); cyInstance.zoom(1); cyInstance.center(); } }
export function zoomIn() { if (cyInstance) cyInstance.zoom({ level: cyInstance.zoom() * 1.2, renderedPosition: { x: cyInstance.width() / 2, y: cyInstance.height() / 2 } }); }
export function zoomOut() { if (cyInstance) cyInstance.zoom({ level: cyInstance.zoom() * 0.8, renderedPosition: { x: cyInstance.width() / 2, y: cyInstance.height() / 2 } }); }

export function setFilters(filters) {
    if (!cyInstance) return;
    const showTypes = filters; // object with booleans
    cyInstance.edges().forEach(e => {
        const t = e.data('type');
        if (t === 'Dependency' && !showTypes.Dependency) e.hide(); else if (t === 'Dependency') e.show();
        if (t === 'Implementation' && !showTypes.Implementation) e.hide(); else if (t === 'Implementation') e.show();
        if (t === 'Inheritance' && !showTypes.Inheritance) e.hide(); else if (t === 'Inheritance') e.show();
    });
}

export function selectNode(id) { if (cyInstance) { const n = cyInstance.getElementById(id); if (n) { n.select(); centerOn(id); spotlight(id); } } }

function centerOn(id) { if (!cyInstance) return; const n = cyInstance.getElementById(id); if (n) cyInstance.animate({ center: { eles: n }, duration: 300 }); }

function spotlight(id) {
    if (!cyInstance) return;
    cyInstance.elements().addClass('faded');
    const center = cyInstance.getElementById(id);
    if (!center) return;
    const neighborhood = center.openNeighborhood().add(center);
    neighborhood.removeClass('faded');
    neighborhood.addClass('highlighted');
}

export function getViewState() {
    if (!cyInstance) return null;
    return { zoom: cyInstance.zoom(), pan: cyInstance.pan(), selected: cyInstance.$(':selected').length ? cyInstance.$(':selected')[0].id() : null };
}

export function setViewState(state) {
    if (!cyInstance || !state) return;
    try { cyInstance.zoom(state.zoom); cyInstance.pan(state.pan); if (state.selected) selectNode(state.selected); } catch { }
}

export function destroy() { if (cyInstance) { cyInstance.destroy(); cyInstance = null; } }
