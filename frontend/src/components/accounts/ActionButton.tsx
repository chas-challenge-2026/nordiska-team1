type ActionButtonProps = {
    style?: "regular" | "light" | "cancel"; 
    onClick: () => void;
    disabled?: boolean;
    isPending?: boolean;
    ariaLabel?: string;
    prefixIcon?: string;
    title: string;
}

export default function ActionButton({
    style = "regular",
    onClick,
    disabled = false,
    isPending = false,
    ariaLabel,
    prefixIcon,
    title,
}: ActionButtonProps) {

    let bgColor = "bg-primary-blue";
    let hoverBgColor = "hover:bg-nordiska-blue"
    let textColor = "text-white"
    let hoverTextColor = "hover:text-white"
    let border = ""
    let iconColor = "bg-white";
    let hoverIconColor = "group-hover:bg-white"

    switch (style) {
        case "light":
            bgColor = "bg-white"
            hoverBgColor = "hover:bg-primary-blue"
            textColor = "text-primary-blue"
            hoverTextColor = "hover:text-white"
            border = "border border-primary-blue"
            iconColor = "bg-primary-blue";
            hoverIconColor = "group-hover:bg-white"
            break;
            

        case "cancel":
            bgColor = "bg-white"
            hoverBgColor = "hover:bg-error"
            textColor = "text-error"
            hoverTextColor = "hover:text-white"
            border = "border border-error"
            iconColor = "bg-error";
            hoverIconColor = "group-hover:bg-white"
            break;

        case "regular":
        default:
            break;
        
    }



    return (

        <button
            type="button"
            disabled={disabled || isPending}
            onClick={onClick}
            aria-label={ariaLabel}
            className={`inline-flex min-h-11 group items-center justify-center rounded-lg ${bgColor} ${border} px-4 py-2.5 text-sm font-semibold ${textColor} transition ${disabled || isPending ? "cursor-not-allowed opacity-50" : "cursor-pointer"} ${hoverBgColor} ${hoverTextColor} focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2 [direction:ltr]`}>

            { prefixIcon &&
                <span aria-hidden="true" style={{maskImage: `url("${prefixIcon}")`, WebkitMaskImage: `url("${prefixIcon}")`,}} className={`mr-2 h-6 w-6  mask-contain   mask-center mask-no-repeat ${iconColor} ${hoverIconColor}`}/>
            }
            
                {title}
        </button>
    )

}