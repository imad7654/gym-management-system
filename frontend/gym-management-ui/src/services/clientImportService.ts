import axiosInstance from '@lib/axios';
import {
  ApiResponse,
  MemberImportPreview,
  MemberImportResult,
} from '@app-types/index';

/**
 * The two-step member import (blueprint 6.3).
 *
 * The file is uploaded twice on purpose. Checking and importing are separate calls, and
 * the second one re-sends the bytes rather than a list of rows the browser parsed - so
 * what gets written is what the server itself read and approved, not whatever the page
 * happens to be holding. The hash from the preview ties the two calls to one file.
 */
export const clientImportService = {
  preview: async (file: File): Promise<MemberImportPreview> => {
    const form = new FormData();
    form.append('file', file);

    const response = await axiosInstance.post<ApiResponse<MemberImportPreview>>(
      '/clients/import/preview',
      form
    );
    return response.data.data!;
  },

  commit: async (
    file: File,
    fileHash: string,
    acknowledgeSkipped: boolean
  ): Promise<MemberImportResult> => {
    const form = new FormData();
    form.append('file', file);
    form.append('fileHash', fileHash);
    form.append('acknowledgeSkipped', String(acknowledgeSkipped));

    const response = await axiosInstance.post<ApiResponse<MemberImportResult>>(
      '/clients/import/commit',
      form
    );
    return response.data.data!;
  },

  /**
   * Fetched through axios rather than linked directly, because the endpoint needs the
   * bearer token that a plain anchor would not send.
   */
  downloadTemplate: async (): Promise<void> => {
    const response = await axiosInstance.get('/clients/import/template', {
      responseType: 'blob',
    });

    const url = URL.createObjectURL(response.data as Blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'fit-bear-members-template.csv';
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  },
};
