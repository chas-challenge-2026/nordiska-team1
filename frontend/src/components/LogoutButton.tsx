import { useLogout } from "../hooks/useLogout";
import { useUserStore } from "../store/userStore";
import { useNavigate } from "react-router";

type LogoutButtonProps = {
    title: string;
}

export default function LogoutButton({ title }: LogoutButtonProps) {
    const { mutate } = useLogout();
    const clearUser = useUserStore((state) => state.logout);
    const navigate = useNavigate();

    function handleLogout() {
        mutate(undefined, {
            onSuccess: () => {
                clearUser();
                navigate("/login");
            },
        });
    }
    return (
        <button
            type="button"
            onClick={() => {
                handleLogout();
            }}
            className="
                uppercase
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
                font-regular
            "
        >
            <span className="
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
                after:scale-x-0

                hover:after:scale-x-100
            ">
                {title}
            </span>
        </button>
    );
}
