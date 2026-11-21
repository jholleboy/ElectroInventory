const shapes = {
 bottle: '<path d="M10 3h4v5l3 4v7a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2v-7l3-4zM7 14h10M10 5h4"/>',
 can: '<rect x="7" y="4" width="10" height="16" rx="2"/><path d="M7 7h10m-10 10h10m-6-5 2-2-1 4"/>',
 cup: '<path d="M4 8h13v6a4 4 0 0 1-4 4H8a4 4 0 0 1-4-4zM17 9h2a2 2 0 0 1 0 4h-2M3 21h17M9 5V3m4 2V2"/>',
 food: '<path d="M5 20V11a3 3 0 0 1 1-6 14 14 0 0 1 12 0 3 3 0 0 1 1 6v9zM9 10l1 2m4-2 1 2"/>',
 burger: '<path d="M4 10a8 7 0 0 1 16 0zM4 14h16M4 17h16l-2 3H6zM10 7h.1m4 0h.1"/>',
 box: '<path d="m4 7 8-4 8 4v11l-8 4-8-4zm0 0 8 4 8-4m-8 4v11M8 5l8 4"/>',
 pill: '<path d="M7 19a4 4 0 0 1-2-7l7-7a4 4 0 0 1 6 6l-7 7a4 4 0 0 1-4 1zM8 9l6 6"/>',
 armor: '<path d="m8 4-4 2-2 5 4 2v7h12v-7l4-2-2-5-4-2-1 4H9zM8 13h8v4H8z"/>',
 torch: '<path d="M4 6h7l4 4h6v4h-6l-4 4H4zM8 6v12m9-8v4"/>',
 tool: '<path d="m13 10 7 8-2 2-8-7m3-3 1-4 4-2-1 4 3 1 2-3-1 6-4 1M10 13 6 21l-3-2 5-8-1-4 4-4v4l2 3"/>',
 phone: '<rect x="7" y="2" width="10" height="20" rx="2"/><path d="M9 6h6v8H9zm2 13h2"/>',
 watch: '<circle cx="12" cy="12" r="5"/><path d="M9 7l1-5h4l1 5m-6 10 1 5h4l1-5m-3-8v3l2 1"/>',
 weapon: '<path d="M3 6h18v5H11l-2 8H5l1-8H3zM12 11v3h-2"/>',
 cash: '<rect x="3" y="6" width="18" height="12" rx="2"/><circle cx="12" cy="12" r="2.5"/><path d="M6 12h.1m12 0h.1"/>',
 car: '<path d="m4 11 2-5h12l2 5m-16 0h16l1 2v5H3v-5zm2 7v2m12-2v2M6 14h2m8 0h2"/>'
};
export function icon(name) {
 return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.45" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' + (shapes[name] ?? shapes.box) + '</svg>';
}
