import { NavLink } from "react-router";

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
                flex items-center justify-center
                whitespace-nowrap
                no-underline

                font-montserrat
                text-lg
                text-white
                uppercase
                tracking-[0.18em]

                transition-all
                duration-200
                ease-in-out

                after:content-['']
                after:absolute
                after:bottom-9
                after:left-1/2
                after:-translate-x-1/2
                after:w-[70%]
                after:h-[3px]
                after:bg-nordiska-orange
                after:origin-center
                after:transition-transform
                after:duration-200
                after:ease-in-out

                ${isActive
                    ? "font-bold after:scale-x-100"
                    : "font-regular after:scale-x-0"
                }

                hover:after:scale-x-100
            `}
        >
            {title}
        </NavLink>
    );
}