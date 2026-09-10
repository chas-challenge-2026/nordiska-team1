import { NavLink } from "react-router";
import capitalize from "../utils/capitalize";

type PageLinkProps = {
    title: string;
    route: string;
};

export default function PageLink({ title, route }: PageLinkProps) {
    return (
        <NavLink
            to={route}
            className={({ isActive }) => `
                relative
                flex
                items-end
                h-full
                
                whitespace-nowrap
                no-underline
                font-montserrat
                cursor-pointer
                transition-all
                duration-200
                ease-in-out
                ${isActive ? "font-bold" : "font-regular"}
            `}
        >
            {({ isActive }) => (
                <span className={`
                    relative
                    inline-block

                    after:content-['']
                    after:absolute
                    after:top-full
                    after:mt-1
                    after:left-1/2
                    after:-translate-x-1/2
                    after:h-[3px]
                    after:bg-nordiska-orange
                    after:origin-center
                    after:transition-all
                    after:duration-200
                    after:ease-in-out

                    ${isActive
                        ? "after:w-full"
                        : "after:w-0 hover:after:w-[70%]"
                    }
                `}>
                    {capitalize(title)}
                </span>
            )}
        </NavLink>
    );
}