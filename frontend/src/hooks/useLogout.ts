import { useMutation } from "@tanstack/react-query";
import { logout } from "../services/authService";

export function useLogout() {
    return useMutation({ mutationFn: logout });
}
