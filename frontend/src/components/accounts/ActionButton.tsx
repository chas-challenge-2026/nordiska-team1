
type ActionButtonProps = {
    onClick: () => void;
    disabled?: boolean;
    isPending?: boolean;
    ariaLabel?: string;

    prefixIcon?: string;
    prefixIconColor?: string;
    prefixIconHoverColor?: string;
    title: string;

    bgColor?: string;
    hoverBgColor?: string;
    textColor?: string;
    hoverTextColor?: string;
}

export default function ActionButton({
    onClick,
    disabled = false,
    isPending = false,
    ariaLabel,
    prefixIcon,
    prefixIconColor = "bg-white",
    prefixIconHoverColor = "group-hover:bg-white",
    title,
    bgColor = "bg-primary-blue",
    hoverBgColor = "hover:bg-nordiska-blue",
    textColor = "text-white",
    hoverTextColor,
}: ActionButtonProps) {


    return (

        <button
            type="button"
            disabled={disabled || isPending}
            onClick={onClick}
            aria-label={ariaLabel}
            className={`inline-flex min-h-11 group items-center justify-center rounded-lg ${bgColor} px-4 py-2.5 text-sm font-semibold ${textColor} transition cursor-pointer ${hoverBgColor} ${hoverTextColor ? hoverTextColor : ""} focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2 [direction:ltr]`}>

            { prefixIcon &&
                <span aria-hidden="true" style={{maskImage: `url("${prefixIcon}")`, WebkitMaskImage: `url("${prefixIcon}")`,}} className={`mr-2 h-6 w-6  mask-contain   mask-center mask-no-repeat ${prefixIconColor} ${prefixIconHoverColor}`}/>
            }
            
                {title}
        </button>
    )

}