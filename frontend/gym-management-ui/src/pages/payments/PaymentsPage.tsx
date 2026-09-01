import { useState } from 'react';
import {
  Container,
  Typography,
  Box,
  Button,
  Paper,
  Chip,
  ChipProps,
  TextField,
  MenuItem,
  Grid,
  IconButton,
  Tooltip,
} from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { paymentService } from '@services/paymentService';
import AddIcon from '@mui/icons-material/Add';
import ReplayIcon from '@mui/icons-material/Replay';
import type { PaymentListItem, PaymentQueryParams, PaymentMethodString, TransactionStatusString } from '../../types/index';
import { PaymentFormDialog, ReversePaymentDialog } from '@components/payments';
import { ResponsiveTable } from '@components/common';

const PaymentsPage = () => {
  const [queryParams, setQueryParams] = useState<PaymentQueryParams>({
    page: 1,
    pageSize: 10,
  });
  const [openFormDialog, setOpenFormDialog] = useState(false);
  const [openReverseDialog, setOpenReverseDialog] = useState(false);
  const [paymentToReverse, setPaymentToReverse] = useState<PaymentListItem | null>(null);

  const { data: paymentsData, isLoading } = useQuery({
    queryKey: ['payments', queryParams],
    queryFn: () => paymentService.getPayments(queryParams),
  });

  const handleReverse = (payment: PaymentListItem) => {
    setPaymentToReverse(payment);
    setOpenReverseDialog(true);
  };

  const getStatusColor = (status: string): ChipProps['color'] => {
    switch (status) {
      case 'Completed':
        return 'success';
      case 'Pending':
        return 'warning';
      case 'Failed':
        return 'error';
      case 'Refunded':
        return 'default';
      default:
        return 'default';
    }
  };

  return (
    <Container maxWidth="lg" sx={{ mt: 4, mb: 4 }}>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h4">Payments</Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpenFormDialog(true)}>
          Record Payment
        </Button>
      </Box>

      <Paper sx={{ p: 2, mb: 2 }}>
        <Grid container spacing={2}>
          <Grid item xs={12} sm={4} md={3}>
            <TextField
              fullWidth
              select
              label="Status"
              value={queryParams.status || ''}
              onChange={(e) =>
                setQueryParams({
                  ...queryParams,
                  page: 1,
                  status: (e.target.value || undefined) as TransactionStatusString | undefined,
                })
              }
            >
              <MenuItem value="">All Statuses</MenuItem>
              <MenuItem value="Completed">Completed</MenuItem>
              <MenuItem value="Pending">Pending</MenuItem>
              <MenuItem value="Failed">Failed</MenuItem>
              <MenuItem value="Refunded">Refunded</MenuItem>
            </TextField>
          </Grid>
          <Grid item xs={12} sm={4} md={3}>
            <TextField
              fullWidth
              select
              label="Payment Method"
              value={queryParams.paymentMethod || ''}
              onChange={(e) =>
                setQueryParams({
                  ...queryParams,
                  page: 1,
                  paymentMethod: (e.target.value || undefined) as PaymentMethodString | undefined,
                })
              }
            >
              <MenuItem value="">All Methods</MenuItem>
              <MenuItem value="Cash">Cash</MenuItem>
              <MenuItem value="Whish">Whish Money</MenuItem>
              <MenuItem value="Other">Other</MenuItem>
            </TextField>
          </Grid>
          <Grid item xs={6} sm={4} md={3}>
            <TextField
              fullWidth
              type="date"
              label="From"
              value={queryParams.startDate || ''}
              onChange={(e) =>
                setQueryParams({ ...queryParams, page: 1, startDate: e.target.value || undefined })
              }
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
          <Grid item xs={6} sm={4} md={3}>
            <TextField
              fullWidth
              type="date"
              label="To"
              value={queryParams.endDate || ''}
              onChange={(e) =>
                setQueryParams({ ...queryParams, page: 1, endDate: e.target.value || undefined })
              }
              InputLabelProps={{ shrink: true }}
            />
          </Grid>
        </Grid>
      </Paper>

      <ResponsiveTable<PaymentListItem>
        rows={paymentsData?.items ?? []}
        rowKey={(payment) => payment.id}
        isLoading={isLoading}
        emptyMessage="No payments match these filters"
        columns={[
          {
            header: 'Amount',
            primary: true,
            render: (p) =>
              p.isReversal
                ? `-$${Math.abs(p.amount).toFixed(2)}`
                : `$${p.amount.toFixed(2)}`,
          },
          {
            header: 'Status',
            badge: true,
            render: (p) =>
              p.isReversal ? (
                <Chip label="Refund" color="error" size="small" variant="outlined" />
              ) : (
                <Chip label={p.status} color={getStatusColor(p.status)} size="small" />
              ),
          },
          { header: 'Member', render: (p) => p.clientName },
          { header: 'Package', render: (p) => p.packageName },
          { header: 'Date', render: (p) => new Date(p.paymentDate).toLocaleDateString() },
          {
            header: 'Method',
            render: (p) => (p.paymentMethod === 'Whish' ? 'Whish Money' : p.paymentMethod),
          },
          {
            header: 'Actions',
            actions: true,
            render: (p) => (
              <Tooltip
                title={
                  p.isReversal
                    ? 'This row is a reversal of another payment'
                    : p.isReversed
                      ? 'This payment has already been refunded'
                      : 'Reverse this payment'
                }
              >
                <span>
                  <IconButton
                    size="small"
                    color="warning"
                    aria-label={`Reverse payment from ${p.clientName}`}
                    // Also disabled once a reversal cancels this row. The server refuses
                    // the second one, but reception should not be able to click it.
                    disabled={p.isReversal || p.isReversed}
                    onClick={() => handleReverse(p)}
                  >
                    <ReplayIcon />
                  </IconButton>
                </span>
              </Tooltip>
            ),
          },
        ]}
      />

      <Box sx={{ mt: 2, display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Typography variant="body2" color="text.secondary">
          Showing {paymentsData?.items.length || 0} of {paymentsData?.totalCount || 0} payments
        </Typography>
        <Box>
          <Button
            disabled={!paymentsData?.hasPreviousPage}
            onClick={() => setQueryParams({ ...queryParams, page: (queryParams.page || 1) - 1 })}
          >
            Previous
          </Button>
          <Button
            disabled={!paymentsData?.hasNextPage}
            onClick={() => setQueryParams({ ...queryParams, page: (queryParams.page || 1) + 1 })}
          >
            Next
          </Button>
        </Box>
      </Box>

      <PaymentFormDialog open={openFormDialog} onClose={() => setOpenFormDialog(false)} />

      <ReversePaymentDialog
        open={openReverseDialog}
        onClose={() => {
          setOpenReverseDialog(false);
          setPaymentToReverse(null);
        }}
        payment={paymentToReverse}
      />
    </Container>
  );
};

export default PaymentsPage;
