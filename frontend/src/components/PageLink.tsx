import { NavLink } from "react-router";

type PageLinkProps = {
    title: string;
    route: string;
    mobile?: string;
    hamburger?: boolean;
};

export default function PageLink({ title, route, mobile, hamburger = false}: PageLinkProps) {
    return (
        <NavLink
            to={route}
            className={({ isActive }) => ` relative flex h-full whitespace-nowrap no-underline font-montserrat cursor-pointer transition-all duration-200 ease-in-out
                ${isActive && !mobile 
                    ? "font-semibold text-white" 
                    : mobile && !isActive ? "font-normal text-white/70" : "font-normal text-white"}
                ${mobile 
                    ? "text-[9px] truncate items-center" 
                    : hamburger 
                        ? "text-base"  
                        : "text-sm tracking-wider uppercase items-end"}`}
        >
            {({ isActive }) => (
                <span className={` 
                    ${hamburger ? "after:mt-1" : "after:mt-0"}
                    ${hamburger ? "after:h-[2px]" : "after:h-[1px]"}
                    ${!mobile || hamburger 
                        ? `relative inline-block after:content-[''] after:absolute after:top-full  after:left-1/2 after:-translate-x-1/2  after:bg-nordiska-orange after:origin-center after:transition-all after:duration-200 after:ease-in-out
                            ${isActive
                                ? "after:w-full after:bg-nordiska-orange"
                                : "after:w-0 after:bg-white hover:after:w-[70%]"
                            }`
                        : ""}`}>

                    {mobile ? (
                    <>
                    <span aria-hidden="true" className={`mx-auto block h-7 w-7 ${isActive ? "bg-white" : "bg-white/70" } ${mobile} mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5`}/>
                        {title}
                    </>
                    ):(
                    <>
                        {title}
                    </>)}        
                </span>
            )}
        </NavLink>
    );
}