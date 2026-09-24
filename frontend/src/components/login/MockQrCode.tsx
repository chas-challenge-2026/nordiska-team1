const SIZE = 25;

// De tre stora "ögonen" i hörnen som gör att det ser ut som en QR-kod
function isFinder(x: number, y: number) {
    const corners = [[0, 0], [SIZE - 7, 0], [0, SIZE - 7]];
    return corners.some(([cx, cy]) => {
        const dx = x - cx;
        const dy = y - cy;
        if (dx < 0 || dx > 6 || dy < 0 || dy > 6) return false;
        const ring = Math.min(dx, dy, 6 - dx, 6 - dy);
        return ring !== 1;
    });
}

function isNearFinder(x: number, y: number) {
    return (x < 8 && y < 8) || (x >= SIZE - 8 && y < 8) || (x < 8 && y >= SIZE - 8);
}

// Fast "slumpmönster" så att koden ser likadan ut varje gång
function isDataModule(x: number, y: number) {
    const n = Math.sin(x * 12.9898 + y * 78.233) * 43758.5453;
    return n - Math.floor(n) > 0.5;
}

const modules: [number, number][] = [];
for (let y = 0; y < SIZE; y++) {
    for (let x = 0; x < SIZE; x++) {
        const filled = isNearFinder(x, y) ? isFinder(x, y) : isDataModule(x, y);
        if (filled) modules.push([x, y]);
    }
}

/**
 * Mock av en BankID-QR-kod. Den går inte att skanna och är inte kopplad
 * till något - den finns bara för att inloggningen ska se ut som BankID.
 */
export default function MockQrCode({ className }: { className?: string }) {
    return (
        <svg
            viewBox={`-2 -2 ${SIZE + 4} ${SIZE + 4}`}
            role="img"
            aria-label="QR-kod"
            shapeRendering="crispEdges"
            className={className}
        >
            <rect x="-2" y="-2" width={SIZE + 4} height={SIZE + 4} fill="white" />
            {modules.map(([x, y]) => (
                <rect key={`${x}-${y}`} x={x} y={y} width="1" height="1" fill="currentColor" />
            ))}
        </svg>
    );
}
