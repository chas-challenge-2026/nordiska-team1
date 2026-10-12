import { useState } from "react";

const STORAGE_KEY = "help-bubble-position";

type Position = {
    x: number;
    y: number;
};

function getInitialPosition(): Position {
    const saved = localStorage.getItem(STORAGE_KEY);

    if (saved) {
        try {return JSON.parse(saved);} 
        catch {
            // använder standardposition om sparad position inte fungerar
        }
    }

    return {
        x: window.innerWidth - 56 - 12,
        y: window.innerHeight - 56 - 76,
    };
}

export function useHelpCardBubblePosition() {
    const [position, setPosition] = useState<Position>(getInitialPosition);

    const updatePosition = (newPosition: Position) => {
        setPosition(newPosition);
        localStorage.setItem(STORAGE_KEY, JSON.stringify(newPosition));
    };

    return {
        position,
        updatePosition,
    };
}