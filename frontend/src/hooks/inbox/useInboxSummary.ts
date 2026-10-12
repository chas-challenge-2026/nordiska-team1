import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
    getUnifiedInbox,
    getUnifiedOverview,
    getUnreadCount,
    markAllItemsAsRead,
    markOneUnifiedAsRead,
} from "../../services/inboxService";
import type { FeedItemTypes } from "../../types/inbox";
import { inboxKeys } from "./inboxKeys";

/** Full counts object (threads, notifications, documents, terms). */
export function useInboxCounts() {
    return useQuery({
        queryKey: inboxKeys.unreadCount(),
        queryFn: getUnreadCount,
    });
}

/** Single number for badge. Same request and cache as useInboxCounts. */
export function useUnreadCount() {
    return useQuery({
        queryKey: inboxKeys.unreadCount(),
        queryFn: getUnreadCount,
        select: (counts) => counts.unreadCount,
    });
}

export function useInboxOverview(enabled = true) {
    return useQuery({
        queryKey: inboxKeys.overview(),
        queryFn: getUnifiedOverview,
        enabled,
    });
}

export function useInboxFeed(
    page: number,
    pageSize: number,
    type?: FeedItemTypes,
    unreadOnly?: boolean,
    enabled = true,
) {
    return useQuery({
        queryKey: inboxKeys.feedList(page, pageSize, type, unreadOnly),
        queryFn: () => getUnifiedInbox(page, pageSize, type, unreadOnly),
        placeholderData: keepPreviousData,
        enabled,
    });
}

/** Marks one feed item read. Item can be any type, so everything in inbox is refreshed. */
export function useMarkFeedItemAsRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: string) => markOneUnifiedAsRead(id),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.all }),
    });
}

/** Marks everything read across all types. */
export function useMarkAllItemsAsRead() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: () => markAllItemsAsRead(),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.all }),
    });
}
