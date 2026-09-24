type IconProps = {
    className?: string;
};

/**
 * Platshållare för BankID-märket. Byt mot den officiella SVG:n från
 * BankID:s grafiska profil (bankid.com/utvecklare) när vi har hämtat den.
 */
export function BankIdLogo({ className }: IconProps) {
    return (
        <svg
            viewBox="0 0 120 96"
            fill="none"
            role="img"
            aria-label="BankID"
            className={className}
        >
            <rect x="34" y="4" width="52" height="56" rx="12" fill="currentColor" />
            <text
                x="60"
                y="46"
                textAnchor="middle"
                fontFamily="Montserrat, sans-serif"
                fontSize="34"
                fontWeight="800"
                fontStyle="italic"
                fill="white"
            >
                iD
            </text>
            <text
                x="60"
                y="88"
                textAnchor="middle"
                fontFamily="Montserrat, sans-serif"
                fontSize="22"
                fontWeight="800"
                fontStyle="italic"
                fill="currentColor"
            >
                BankID
            </text>
        </svg>
    );
}

export function PhoneIcon({ className }: IconProps) {
    return (
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true" className={className}>
            <rect x="6" y="2" width="12" height="20" rx="2" stroke="currentColor" strokeWidth="2" />
            <path d="M11 18h2" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
        </svg>
    );
}

export function DesktopIcon({ className }: IconProps) {
    return (
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true" className={className}>
            <rect x="4" y="4" width="16" height="11" rx="1" stroke="currentColor" strokeWidth="2" />
            <path d="M2 19h20" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
        </svg>
    );
}

export function ArrowRightIcon({ className }: IconProps) {
    return (
        <svg viewBox="0 0 24 24" fill="none" aria-hidden="true" className={className}>
            <path
                d="M4 12h15M13 6l6 6-6 6"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
            />
        </svg>
    );
}
