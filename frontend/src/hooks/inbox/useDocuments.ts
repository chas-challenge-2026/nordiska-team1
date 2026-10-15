import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
    downloadInboxDocument,
    getAllArchivedDocuments,
    getAllGeneralDocuments,
} from "../../services/inboxService";
import { inboxKeys, invalidateSummary } from "./inboxKeys";

export function useArchivedDocuments(
    page: number,
    pageSize: number,
    year?: number,
    documentType?: string,
    enabled = true,
) {
    return useQuery({
        queryKey: inboxKeys.documentList(page, pageSize, year, documentType),
        queryFn: () => getAllArchivedDocuments(page, pageSize, year, documentType),
        placeholderData: keepPreviousData,
        enabled,
    });
}

export function useGeneralDocuments(enabled = true) {
    return useQuery({
        queryKey: inboxKeys.generalDocuments(),
        queryFn: getAllGeneralDocuments,
        enabled,
    });
}

export function useDownloadInboxDocument() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: number) => downloadInboxDocument(id),
        onSuccess: () =>
            Promise.all([
                queryClient.invalidateQueries({ queryKey: inboxKeys.documents() }),
                invalidateSummary(queryClient),
            ]),
    });
}
