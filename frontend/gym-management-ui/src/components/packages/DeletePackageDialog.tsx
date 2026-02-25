import { useState } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Alert,
  Snackbar,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { packageService } from '@services/packageService';
import type { Package } from '../../types';

interface DeletePackageDialogProps {
  open: boolean;
  onClose: () => void;
  package: Package | null;
}

export const DeletePackageDialog = ({ open, onClose, package: pkg }: DeletePackageDialogProps) => {
  const queryClient = useQueryClient();
  const [showSuccess, setShowSuccess] = useState(false);

  const deleteMutation = useMutation({
    mutationFn: (id: number) => packageService.deletePackage(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['packages'] });
      setShowSuccess(true);
      setTimeout(() => {
        onClose();
      }, 1000);
    },
  });

  const handleDelete = () => {
    if (pkg) {
      deleteMutation.mutate(pkg.id);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Delete Package</DialogTitle>
      <DialogContent>
        {deleteMutation.isError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            Failed to delete package. Please try again.
          </Alert>
        )}
        <Typography>
          Are you sure you want to delete <strong>{pkg?.name}</strong>?
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
          This action will mark the package as inactive. You can restore it later if needed.
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
      <Snackbar
        open={showSuccess}
        autoHideDuration={3000}
        onClose={() => setShowSuccess(false)}
        message="Package deleted successfully!"
      />
    </Dialog>
  );
};
