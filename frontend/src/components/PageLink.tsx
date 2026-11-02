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
            className={({ isActive }) => `relative flex h-full whitespace-nowrap no-underline cursor-pointer transition-all duration-200 ease-in-out
                ${ mobile ? "text-[11px] sm:text-xs items-center" : ""}
                ${mobile
                    ? isActive ? "font-semibold text-white" : "text-light-blue-accent"
                    : hamburger
                        ? "text-white font-medium"
                        : isActive
                            ? "text-white font-bold"
                            : "text-light-blue-accent font-medium"
                }
            `}
        >
            {({ isActive }) => (
                <span className={` 
                    ${hamburger ? "after:mt-1" : "after:mt-0"}
                    ${hamburger ? "after:h-[2px]" : "after:h-[2px]"}
                    ${!mobile || hamburger 
                        ? `relative inline-block after:content-[''] after:absolute after:top-full  after:left-1/2 after:-translate-x-1/2  after:bg-light-blue-accent after:origin-center after:transition-all after:duration-200 after:ease-in-out
                            ${isActive
                                ? "after:w-full after:bg-light-blue-accent"
                                : "after:w-0 after:bg-white hover:after:w-[70%]"
                            }`
                        : ""}   
                    `}>

                    {mobile ? (
                    <>
                    <span aria-hidden="true" className={`mx-auto block h-5 w-5 sm:h-7 sm:w-7 ${isActive ? "bg-white" : "bg-light-blue-accent/90" } ${mobile} mask-contain mask-center mask-no-repeat `}/>
                        {title}
                    </>
                    ):(
                        title
                    )}        
                </span>
            )}
        </NavLink>
    );
}