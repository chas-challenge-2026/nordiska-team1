import { useMutation } from "@tanstack/react-query";
import { updateCustomerOverviewLayout } from "../services/customerService";
import { useUserStore } from "../store/userStore";
import type { CardId } from "../components/overview/useCardOrder";

export function useUpdateOverviewLayout() {
    const user = useUserStore((state) => state.user);
    const updateUser = useUserStore((state) => state.updateUser);

    return useMutation({
        mutationFn: async (order: CardId[]) => {
            if (!user?.id) { throw new Error("No authenticated user found");}
            await updateCustomerOverviewLayout(user.id, order);
        },
        onSuccess: (_, order) => {
            updateUser({
                overviewPreference: order,
            });
        },
    });
}