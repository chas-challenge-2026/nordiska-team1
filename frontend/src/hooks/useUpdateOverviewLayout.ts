import { useMutation } from "@tanstack/react-query";
import { updateCustomerOverviewLayout } from "../services/customerService";
import { useUserStore } from "../store/userStore";

export function useUpdateOverviewLayout() {
    const user = useUserStore((state) => state.user);
    const updateUser = useUserStore((state) => state.updateUser);

    return useMutation({
        mutationFn: async (overviewPreference: string[]) => {
            if (!user?.id) { throw new Error("No authenticated user found"); }
            await updateCustomerOverviewLayout(user.id, overviewPreference);
        },
        onSuccess: (_, overviewPreference) => {
            updateUser({
                overviewPreference,
            });
        },
    });
}