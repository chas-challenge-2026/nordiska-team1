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
                h-full
                whitespace-nowrap
                no-underline
                font-montserrat
                text-lg
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
                    after:w-[70%]
                    after:h-[3px]
                    after:bg-nordiska-orange
                    after:origin-center
                    after:transition-transform
                    after:duration-200
                    after:ease-in-out

                    ${isActive ? "after:scale-x-100" : "after:scale-x-0"}
                    hover:after:scale-x-100
                `}>
                    {capitalize(title)}
                </span>
            )}
        </NavLink>
    );
}