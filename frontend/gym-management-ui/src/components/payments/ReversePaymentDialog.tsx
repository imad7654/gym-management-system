import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Typography,
  Alert,
  TextField,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { paymentService } from '@services/paymentService';
import { PaymentListItem } from '@app-types/index';

interface ReversePaymentDialogProps {
  open: boolean;
  onClose: () => void;
  payment: PaymentListItem | null;
}

/**
 * Reverses a payment taken in error - wrong amount, wrong member, wrong package.
 *
 * The original row is left untouched. The server writes a second row cancelling it and
 * takes back the days it bought, so the till still reconciles and the member does not keep
 * time they were refunded for.
 */
export const ReversePaymentDialog = ({ open, onClose, payment }: ReversePaymentDialogProps) => {
  const queryClient = useQueryClient();
  const [reason, setReason] = useState('');

  useEffect(() => {
    if (!open) setReason('');
  }, [open]);

  const reverseMutation = useMutation({
    mutationFn: (id: number) => paymentService.reversePayment(id, reason || undefined),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payments'] });
      queryClient.invalidateQueries({ queryKey: ['clients'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      onClose();
    },
  });

  const errorMessage =
    (reverseMutation.error as { response?: { data?: { message?: string } } } | null)
      ?.response?.data?.message ?? 'Failed to reverse the payment. Please try again.';

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Reverse payment</DialogTitle>
      <DialogContent>
        {reverseMutation.isError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {errorMessage}
          </Alert>
        )}

        <Typography sx={{ mb: 2 }}>
          Reverse <strong>${payment?.amount.toFixed(2)}</strong> from{' '}
          <strong>{payment?.clientName}</strong> for {payment?.packageName}?
        </Typography>

        <Alert severity="info" sx={{ mb: 2 }}>
          The original payment stays on the record. A cancelling entry is added next to it,
          and the days this payment bought are taken off the membership.
        </Alert>

        <TextField
          fullWidth
          label="Reason"
          placeholder="Wrong amount, wrong member, wrong package..."
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          multiline
          rows={2}
          helperText="Optional, but it is what the owner reads months later"
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={reverseMutation.isPending}>
          Cancel
        </Button>
        <Button
          onClick={() => payment && reverseMutation.mutate(payment.id)}
          variant="contained"
          color="warning"
          disabled={reverseMutation.isPending}
        >
          {reverseMutation.isPending ? 'Reversing...' : 'Reverse payment'}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
