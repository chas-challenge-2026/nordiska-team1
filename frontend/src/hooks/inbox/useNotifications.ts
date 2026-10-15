import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
    getAllNotifications,
    markAllNotificationsAsRead,
    markNotificationAsRead,
} from "../../services/inboxService";
import { inboxKeys, invalidateSummary } from "./inboxKeys";

export function useNotifications(unreadOnly: boolean, page: number, pageSize: number, enabled = true) {
    return useQuery({
        queryKey: inboxKeys.notificationList(unreadOnly, page, pageSize),
        queryFn: () => getAllNotifications(unreadOnly, page, pageSize),
        placeholderData: keepPreviousData,
        enabled,
    });
}

export function useMarkNotificationAsRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (notificationId: number) => markNotificationAsRead(notificationId),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.notifications() }),
                invalidateSummary(queryClient),
            ]),
    });
}

export function useMarkAllNotificationsAsRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: () => markAllNotificationsAsRead(),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.notifications() }),
                invalidateSummary(queryClient),
            ]),
    });
}
