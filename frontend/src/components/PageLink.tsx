import { NavLink } from "react-router";
import capitalize from "../utils/capitalize";

type PageLinkProps = {
    title: string;
    route: string;
    header?: boolean;
};

export default function PageLink({ title, route, header=false }: PageLinkProps) {
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
                ${isActive ? header ? "font-semibold" :"font-bold" : "font-normal"}
            `}
        >
            {({ isActive }) => (
                <span className={`
                    relative
                    inline-block

                    after:content-['']
                    after:absolute
                    after:top-full
                    ${header ? "after:mt-0.5" : "after:mt-0"}
                    after:left-1/2
                    after:-translate-x-1/2
                    ${header ? "after:h-[2px]" : "after:h-[4px]"}
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