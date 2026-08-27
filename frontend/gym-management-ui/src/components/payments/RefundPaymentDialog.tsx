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
import { paymentService } from '@services/paymentService';
import { PaymentListItem } from '@app-types/index';

interface RefundPaymentDialogProps {
  open: boolean;
  onClose: () => void;
  payment: PaymentListItem | null;
}

export const RefundPaymentDialog = ({ open, onClose, payment }: RefundPaymentDialogProps) => {
  const queryClient = useQueryClient();

  const refundMutation = useMutation({
    mutationFn: (id: number) => paymentService.refundPayment(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payments'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      onClose();
    },
  });

  const handleRefund = () => {
    if (payment) {
      refundMutation.mutate(payment.id);
    }
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Refund Payment</DialogTitle>
      <DialogContent>
        {refundMutation.isError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            Failed to refund payment. Please try again.
          </Alert>
        )}
        <Typography>
          Refund <strong>${payment?.amount.toFixed(2)}</strong> paid by{' '}
          <strong>{payment?.clientName}</strong> for {payment?.packageName}?
        </Typography>
        <Alert severity="warning" sx={{ mt: 2 }}>
          This marks the payment as refunded but does not change the client's
          current package or membership dates — update those manually on the
          Clients page if the membership should also be revoked.
        </Alert>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={refundMutation.isPending}>
          Cancel
        </Button>
        <Button
          onClick={handleRefund}
          variant="contained"
          color="warning"
          disabled={refundMutation.isPending}
        >
          {refundMutation.isPending ? 'Refunding...' : 'Refund Payment'}
        </Button>
      </DialogActions>
    </Dialog>
  );
};
