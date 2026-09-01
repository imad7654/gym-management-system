import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Alert,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { clientService } from '@services/clientService';
import { Client } from '@app-types/index';

interface DeleteClientDialogProps {
  open: boolean;
  onClose: () => void;
  /**
   * Only the two fields this dialog actually uses. Asking for a whole Client meant the
   * member list had to cast a list row to one, which hid the fact that the extra fields
   * were never populated.
   */
  client: Pick<Client, 'id' | 'fullName'> | null;
}

export const DeleteClientDialog = ({ open, onClose, client }: DeleteClientDialogProps) => {
  const queryClient = useQueryClient();

  const deleteMutation = useMutation({
    mutationFn: (id: number) => clientService.deleteClient(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['clients'] });
      onClose();
    },
  });

  const handleDelete = () => {
    if (client) {
      deleteMutation.mutate(client.id);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Delete Client</DialogTitle>
      <DialogContent>
        {deleteMutation.isError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            Failed to delete client. Please try again.
          </Alert>
        )}
        <Typography>
          Are you sure you want to delete <strong>{client?.fullName}</strong>?
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
          This action will mark the client as inactive. You can restore them later if needed.
        </Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={deleteMutation.isPending}>
          Cancel
        </Button>
        <Button
          onClick={handleDelete}
          variant="contained"
          color="error"
          disabled={deleteMutation.isPending}
        >
          {deleteMutation.isPending ? 'Deleting...' : 'Delete'}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
