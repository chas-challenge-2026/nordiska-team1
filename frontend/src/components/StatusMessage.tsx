type StatusMessageProps = {
    title?: string;
    message?: string;
};

export function LoadingState({ title, message}: StatusMessageProps) {
    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-15 w-15 animate-[spin_3s_steps(8)_infinite] bg-nordiska-orange mask-[url('/icons/throbber.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            {title && (<p className="font-semibold mt-1 text-dark-navy/80"> {title} </p>)}
            {message && (<p className="font-semibold mt-1 text-dark-navy/80"> {message} </p>)}
        </div>
    );
}

export function ErrorState({ title, message}: StatusMessageProps) {
    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-15 w-15 bg-red-700 mask-[url('/icons/frown.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            {title && (<p className="font-semibold mt-1 text-dark-navy/80"> {title} </p>)}
            {message && (<p className="font-semibold mt-1 text-dark-navy/80"> {message} </p>)}
        </div>
    );
}