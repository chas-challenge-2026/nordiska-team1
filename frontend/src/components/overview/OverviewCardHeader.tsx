type OverviewCardHeaderProps = {
    title: string;
    description?: string;
}

export default function OverviewCardHeader({title, description}: OverviewCardHeaderProps) {

    return (
        <div className="h-16 shrink-0 bg-dark-navy pl-1.5 pt-1 text-white ">
            <div className="flex flex-col items-start p-2">
                <h2 title={title} className="w-full text-lg font-semibold tracking-wide md:text-xl truncate"> {title} </h2>
                <p className="text-[0.65em] uppercase text-white/80 sm:text-[0.70em]"> {description} </p>
            </div>
        </div>
    );
}