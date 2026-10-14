import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
    createBroadcastMessage,
    publishNewTerms,
    staffGetAllThreads,
    staffGetThread,
    staffSendReply,
} from "../../services/inboxService";
import { inboxKeys, type StaffThreadStatus } from "./inboxKeys";

// ---------- Queries ----------

export function useStaffThreads(
    page: number,
    pageSize: number,
    customerId?: number,
    status?: StaffThreadStatus,
    searchTerm?: string,
    enabled = true,
) {
    return useQuery({
        queryKey: inboxKeys.adminThreadList(page, pageSize, customerId, status, searchTerm),
        queryFn: () => staffGetAllThreads(page, pageSize, customerId, status, searchTerm),
        placeholderData: keepPreviousData,
        enabled,
    });
}

export function useStaffThread(id: number | undefined) {
    return useQuery({
        queryKey: inboxKeys.adminThread(id as number),
        queryFn: () => staffGetThread(id as number),
        enabled: id !== undefined,
    });
}

// ---------- Mutations ----------

export function useStaffSendReply() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: { id: number; body: string; replyAllowed: boolean }) =>
            staffSendReply(vars.id, vars.body, vars.replyAllowed),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
    });
}

export function useCreateBroadcastMessage() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: {
            body: string | null;
            broadcastToAll: boolean;
            category: string | null;
            customerId: number | null;
            isInformationOnly: boolean;
            replyAllowed: boolean;
            subject: string | null;
        }) =>
            createBroadcastMessage(
                vars.body,
                vars.broadcastToAll,
                vars.category,
                vars.customerId,
                vars.isInformationOnly,
                vars.replyAllowed,
                vars.subject,
            ),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.threads() }),
    });
}

export function usePublishNewTerms() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (vars: { code: string; documentId: number; effectiveFrom: string; title: string; version: number }) =>
            publishNewTerms(vars.code, vars.documentId, vars.effectiveFrom, vars.title, vars.version),
        onSuccess: () => queryClient.invalidateQueries({ queryKey: inboxKeys.terms() }),
    });
}
