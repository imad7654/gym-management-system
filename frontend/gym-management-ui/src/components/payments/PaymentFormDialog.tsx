import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  MenuItem,
  Grid,
  Alert,
  Autocomplete,
  CircularProgress,
  InputAdornment,
  Snackbar,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { clientService } from '@services/clientService';
import { packageService } from '@services/packageService';
import { paymentService } from '@services/paymentService';
import { ClientListItem, PaymentMethodMap, PaymentMethodString } from '@app-types/index';

interface PaymentFormDialogProps {
  open: boolean;
  onClose: () => void;
}

const addDays = (dateStr: string, days: number) => {
  const date = new Date(dateStr);
  date.setDate(date.getDate() + days);
  return date.toISOString().split('T')[0];
};

const today = () => new Date().toISOString().split('T')[0];

export const PaymentFormDialog = ({ open, onClose }: PaymentFormDialogProps) => {
  const queryClient = useQueryClient();
  const [showSuccess, setShowSuccess] = useState(false);

  const [clientSearch, setClientSearch] = useState('');
  const [selectedClient, setSelectedClient] = useState<ClientListItem | null>(null);

  const [formData, setFormData] = useState({
    packageId: '' as number | '',
    amount: '',
    paymentDate: today(),
    paymentMethod: 'Cash' as PaymentMethodString,
    periodStartDate: today(),
    periodEndDate: '',
    transactionReference: '',
    notes: '',
  });

  const { data: clientOptions, isFetching: isSearchingClients } = useQuery({
    queryKey: ['clients', 'search', clientSearch],
    queryFn: () => clientService.getClients({ page: 1, pageSize: 20, search: clientSearch }),
    enabled: open,
  });

  const { data: packages } = useQuery({
    queryKey: ['packages', 'active'],
    queryFn: () => packageService.getActivePackages(),
    enabled: open,
  });

  const selectedPackage = packages?.find((p) => p.id === formData.packageId);

  useEffect(() => {
    if (!open) resetForm();
  }, [open]);

  // Auto-fill amount and period end date when the package or start date changes
  useEffect(() => {
    if (selectedPackage) {
      setFormData((prev) => ({
        ...prev,
        amount: prev.amount || selectedPackage.price.toString(),
        periodEndDate: addDays(prev.periodStartDate, selectedPackage.durationDays),
      }));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [formData.packageId, formData.periodStartDate]);

  const resetForm = () => {
    setSelectedClient(null);
    setClientSearch('');
    setFormData({
      packageId: '',
      amount: '',
      paymentDate: today(),
      paymentMethod: 'Cash',
      periodStartDate: today(),
      periodEndDate: '',
      transactionReference: '',
      notes: '',
    });
  };

  const createMutation = useMutation({
    mutationFn: paymentService.createPayment,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['payments'] });
      queryClient.invalidateQueries({ queryKey: ['clients'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      setShowSuccess(true);
      setTimeout(() => {
        onClose();
        resetForm();
      }, 1000);
    },
  });

  const isValid =
    !!selectedClient &&
    !!formData.packageId &&
    Number(formData.amount) > 0 &&
    !!formData.paymentDate &&
    !!formData.periodStartDate &&
    !!formData.periodEndDate;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid || !selectedClient) return;

    createMutation.mutate({
      clientId: selectedClient.id,
      packageId: formData.packageId as number,
      amount: parseFloat(formData.amount),
      paymentDate: formData.paymentDate,
      paymentMethod: formData.paymentMethod,
      periodStartDate: formData.periodStartDate,
      periodEndDate: formData.periodEndDate,
      transactionReference: formData.transactionReference || undefined,
      notes: formData.notes || undefined,
    });
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={handleSubmit}>
        <DialogTitle>Record Payment</DialogTitle>
        <DialogContent>
          {createMutation.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              Failed to record payment. Please try again.
            </Alert>
          )}

          <Alert severity="info" sx={{ mb: 2 }}>
            Recording a payment sets this as the client's current package and renews
            their membership through the period end date below.
          </Alert>

          <Grid container spacing={2} sx={{ mt: 1 }}>
            <Grid item xs={12}>
              <Autocomplete
                options={clientOptions?.items || []}
                getOptionLabel={(option) => `${option.fullName} · ${option.phoneNumber}`}
                isOptionEqualToValue={(option, value) => option.id === value.id}
                value={selectedClient}
                onChange={(_, value) => setSelectedClient(value)}
                inputValue={clientSearch}
                onInputChange={(_, value) => setClientSearch(value)}
                loading={isSearchingClients}
                renderInput={(params) => (
                  <TextField
                    {...params}
                    required
                    label="Client"
                    placeholder="Search by name, phone, or email..."
                    InputProps={{
                      ...params.InputProps,
                      endAdornment: (
                        <>
                          {isSearchingClients ? <CircularProgress size={18} /> : null}
                          {params.InputProps.endAdornment}
                        </>
                      ),
                    }}
                  />
                )}
              />
            </Grid>

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                select
                label="Package"
                value={formData.packageId}
                onChange={(e) =>
                  setFormData({ ...formData, packageId: Number(e.target.value) })
                }
              >
                {packages?.map((pkg) => (
                  <MenuItem key={pkg.id} value={pkg.id}>
                    {pkg.name} — ${pkg.price.toFixed(2)}
                  </MenuItem>
                ))}
              </TextField>
            </Grid>

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                label="Amount"
                type="number"
                value={formData.amount}
                onChange={(e) => setFormData({ ...formData, amount: e.target.value })}
                InputProps={{
                  startAdornment: <InputAdornment position="start">$</InputAdornment>,
                }}
                inputProps={{ min: '0.01', step: '0.01' }}
              />
            </Grid>

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                type="date"
                label="Payment Date"
                value={formData.paymentDate}
                onChange={(e) => setFormData({ ...formData, paymentDate: e.target.value })}
                InputLabelProps={{ shrink: true }}
              />
            </Grid>

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                select
                label="Payment Method"
                value={formData.paymentMethod}
                onChange={(e) =>
                  setFormData({ ...formData, paymentMethod: e.target.value as PaymentMethodString })
                }
              >
                {Object.keys(PaymentMethodMap).map((method) => (
                  <MenuItem key={method} value={method}>
                    {method === 'BankTransfer' ? 'Bank Transfer' : method}
                  </MenuItem>
                ))}
              </TextField>
            </Grid>

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                type="date"
                label="Coverage Start"
                value={formData.periodStartDate}
                onChange={(e) =>
                  setFormData({ ...formData, periodStartDate: e.target.value })
                }
                InputLabelProps={{ shrink: true }}
              />
            </Grid>

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                type="date"
                label="Coverage End"
                value={formData.periodEndDate}
                onChange={(e) =>
                  setFormData({ ...formData, periodEndDate: e.target.value })
                }
                InputLabelProps={{ shrink: true }}
                helperText={selectedPackage ? `Auto-filled from ${selectedPackage.name}'s duration` : ''}
              />
            </Grid>

            <Grid item xs={12}>
              <TextField
                fullWidth
                label="Transaction Reference"
                placeholder="Receipt #, transfer ID, etc. (optional)"
                value={formData.transactionReference}
                onChange={(e) =>
                  setFormData({ ...formData, transactionReference: e.target.value })
                }
              />
            </Grid>

            <Grid item xs={12}>
              <TextField
                fullWidth
                label="Notes"
                value={formData.notes}
                onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
                multiline
                rows={2}
              />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancel</Button>
          <Button
            type="submit"
            variant="contained"
            disabled={!isValid || createMutation.isPending}
          >
            {createMutation.isPending ? 'Recording...' : 'Record Payment'}
          </Button>
        </DialogActions>
      </form>
      <Snackbar
        open={showSuccess}
        autoHideDuration={3000}
        onClose={() => setShowSuccess(false)}
        message="Payment recorded successfully!"
      />
    </Dialog>
  );
};
