type StatusMessageProps = {
    title?: string;
    message?: string;
};

export function LoadingState({ title, message}: StatusMessageProps) {
    return (
        <div className="h-full flex flex-col items-center justify-center">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 animate-[spin_1.5s_steps(8)_infinite] bg-nordiska-orange mask-[url('/icons/throbber.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            {title && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {title} </p>)}
            {message && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {message} </p>)}
        </div>
    );
}

export function ErrorState({ title, message}: StatusMessageProps) {
    return (
        <div className="h-full flex flex-col items-center justify-center">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-red-700 mask-[url('/icons/frown.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            {title && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {title} </p>)}
            {message && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {message} </p>)}
        </div>
    );
}