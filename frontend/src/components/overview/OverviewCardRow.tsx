type OverviewCardRowProps = {
    key: number | string;
    btnHoverText?: string;
    onClick: () => void;
    cols: "2" | "3";
    titleHoverText?: string;
    titleRow: React.ReactNode[];
    descriptionRow: React.ReactNode[];
    accountFavorite?: boolean;
    greenAmount?: boolean;
    colorAmount?: "plus" | "minus" | "neutral";
    progressBar?: [boolean, number];
}

export function OverviewCardRow({
        key,
        btnHoverText,
        onClick,
        cols,
        titleHoverText,
        titleRow,
        descriptionRow,
        accountFavorite = false,
        greenAmount = false,
        colorAmount = "neutral",
        progressBar = [false, 0],
    }:OverviewCardRowProps
){

    function progressColor(percent: number){
        if (percent === 100){return "bg-[#16A34A]"} // #16A34A
        else if (percent > 80) return "bg-[#CCC703]" // #CCC703
        else if (percent > 50) return "bg-[#EAB308]" // #EAB308
        else if (percent > 30) return "bg-[#F59E0B]" // #F59E0B
        else return "bg-[#F97316]" // #F97316
    }

    const barColor = progressColor(progressBar[1]);
    const gridCols = cols === "2" ? "grid-cols-[2fr_1fr]" : progressBar[0] ? "grid-cols-[2fr_0.5fr_1.5fr]" : "grid-cols-[2fr_1.5fr_1.5fr]";

    return (
        <button 
            key={key}
            title={btnHoverText}
            type="button"
            onClick={onClick}
            className="flex w-full shrink-0 flex-col justify-center py-3 px-3 xl:py-4 odd:bg-light-gray/60 even:bg-white hover:bg-primary-blue/10 cursor-pointer"
        >

            {/* ----- TITLE ROW ----- */}
            <p className={`w-full grid ${gridCols} items-end text-sm gap-2 xl:text-base`}>
                {/* vänster */}
                <span className="flex min-w-0 items-start gap-1">
                    <span title={titleHoverText} className="min-w-0 max-w-full truncate text-left font-semibold justify-self-start">{titleRow[0]}</span>
                    {accountFavorite && <span aria-hidden="true" className="h-3 w-3 xl:h-4 xl:w-4 mt-1 shrink-0 mask-[url('/icons/star-solid.svg')] mask-contain mask-center mask-no-repeat bg-light-blue-accent"/>}
                </span>
                {/* mitten ? */}
                {cols === "3" &&(
                    <span className={`min-w-0 truncate font-semibold ${greenAmount ? "text-green-700" : "" } justify-self-end`}>{titleRow[1]}</span>
                )}
                {/* höger */}
                <span className={`shrink-0 whitespace-nowrap justify-self-end ${cols === "2" ? "font-semibold" : "font-medium text-sm"} ${colorAmount === "neutral" ? "" : colorAmount === "plus" ? "text-green-700" : "text-red-700"}`}>
                    {colorAmount === "plus" ? "+" : ""}{titleRow.at(-1)}
                </span>
            </p>

            {/* ----- PROGRESS BAR ? ----- */}
            {progressBar[0] && 
                <div className="shrink-0 h-2 my-1 w-full overflow-hidden rounded-full bg-secondary/20">
                    <div role="progressbar" aria-label={String(titleRow[1])} aria-valuemin={0} aria-valuemax={100} aria-valuenow={progressBar[1]} className={`h-full rounded-full ${barColor} transition-[width] duration-300`} style={{ width: `${progressBar[1]}%` }} />
                </div>
            }

            {/* ----- DESCRIPTION ROW ----- */}
            <p className={`grid ${gridCols} text-[9px] uppercase text-dark-navy/60 sm:text-xs`}>
                {/* vänster */}
                <span className="shrink-0 whitespace-nowrap justify-self-start">{descriptionRow[0]}</span>
                {/* mitten ? */}
                {cols === "3" && (<span className="justify-self-end">{descriptionRow[1]}</span>)}
                {/* höger */}
                <span className="shrink-0 whitespace-nowrap justify-self-end">{descriptionRow.at(-1)}</span>
            </p>
        </button>
    );
}


// -----------------------------------
// ---------- FIXED TOP ROW ----------
// -----------------------------------
type OverviewCardFixedRowProps = {
    cols: "2" | "3";
    hoverTitle: string;
    titleRow: React.ReactNode[];
    descriptionRow: React.ReactNode[];
    greenAmount?: boolean;
}

export function OverviewCardFixedRow({cols, hoverTitle, titleRow, descriptionRow, greenAmount = false}: OverviewCardFixedRowProps) {

    const gridCols = cols === "2" ? "grid-cols-[2fr_1fr]" : "grid-cols-[2fr_1.5fr_1.5fr]";

    return (
        <div  title={hoverTitle} className="flex w-full shrink-0 flex-col justify-center py-3 px-3 xl:py-4 bg-primary-blue/10 border-b border-nordiska-blue/50 -mt-[1px]">
            {/* ----- DESCRIPTION ROW ----- */}
            <p className={`grid ${gridCols} text-[9px] uppercase text-dark-navy/60 sm:text-xs`}>
                {/* vänster */}
                <span className="shrink-0 whitespace-nowrap justify-self-start">{descriptionRow[0]}</span>
                {/* mitten ? */}
                {cols === "3" && (<span className="justify-self-end">{descriptionRow[1]}</span>)}
                {/* höger */}
                <span className="shrink-0 whitespace-nowrap justify-self-end">{descriptionRow.at(-1)}</span>
            </p>

            {/* ----- TITLE ROW ----- */}
            <p className={`w-full grid ${gridCols} items-end text-sm gap-2 xl:text-base`}>
                {/* vänster */}
                <span  className="min-w-0 max-w-full truncate text-left font-semibold justify-self-start">{titleRow[0]}</span>
                {/* mitten ? */}
                {cols === "3" &&(<span className={`min-w-0 truncate font-semibold justify-self-end ${greenAmount ? "text-green-700" : "" }`}>{titleRow[1]}</span>)}
                {/* höger */}
                <span className={`shrink-0 whitespace-nowrap justify-self-end ${cols === "2" ? "font-semibold" : "font-medium text-sm"}}`}>{titleRow.at(-1)}</span>
            </p>
        </div>
    );
}