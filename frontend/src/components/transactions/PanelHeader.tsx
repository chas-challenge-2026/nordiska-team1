type PanelHeaderProps = {
    subtitle: string;
    title: string;
};

/** Navy-header för sidopanelerna, samma stil som AccountNavigation på Bankkonton. */
export default function PanelHeader({ subtitle, title }: PanelHeaderProps) {
    return (
        <div className="flex min-h-22 items-center bg-dark-navy px-4 py-3">
            <div className="min-w-0 text-white">
                <p className="truncate text-xs uppercase text-white/70">{subtitle}</p>
                <h2 className="mt-1 truncate text-md font-semibold">{title}</h2>
            </div>
        </div>
    );
}
