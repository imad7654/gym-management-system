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
  ToggleButton,
  ToggleButtonGroup,
  Typography,
  Box,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { clientService } from '@services/clientService';
import { packageService } from '@services/packageService';
import { paymentService } from '@services/paymentService';
import {
  ClientListItem,
  CurrencyString,
  PaymentMethodMap,
  PaymentMethodString,
} from '@app-types/index';

interface PaymentFormDialogProps {
  open: boolean;
  onClose: () => void;
}

/**
 * The desk payment form.
 *
 * Reception enters only what it can actually observe: who is paying, for what, how, and
 * how much changed hands. The price, the membership period and the USD conversion are all
 * worked out by the server from the package. The figures shown below the form are a
 * preview of that calculation, never the source of it.
 */
export const PaymentFormDialog = ({ open, onClose }: PaymentFormDialogProps) => {
  const queryClient = useQueryClient();
  const [showSuccess, setShowSuccess] = useState(false);

  const [clientSearch, setClientSearch] = useState('');
  const [selectedClient, setSelectedClient] = useState<ClientListItem | null>(null);

  const [formData, setFormData] = useState({
    packageId: '' as number | '',
    amountReceived: '',
    currency: 'Usd' as CurrencyString,
    exchangeRate: '',
    paymentMethod: 'Cash' as PaymentMethodString,
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
  const isLbp = formData.currency === 'Lbp';

  const resetForm = () => {
    setSelectedClient(null);
    setClientSearch('');
    setFormData({
      packageId: '',
      amountReceived: '',
      currency: 'Usd',
      exchangeRate: '',
      paymentMethod: 'Cash',
      transactionReference: '',
      notes: '',
    });
  };

  useEffect(() => {
    if (!open) resetForm();
  }, [open]);

  // Prefill what they owe, in whichever currency is selected. Reception can overwrite it -
  // this is a convenience, and the server checks the real figure either way.
  useEffect(() => {
    if (!selectedPackage) return;

    const rate = parseFloat(formData.exchangeRate);
    const suggested = isLbp
      ? rate > 0
        ? Math.round(selectedPackage.price * rate).toString()
        : ''
      : selectedPackage.price.toFixed(2);

    if (suggested) {
      setFormData((prev) => ({ ...prev, amountReceived: suggested }));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [formData.packageId, formData.currency, formData.exchangeRate]);

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

  const received = parseFloat(formData.amountReceived);
  const rate = parseFloat(formData.exchangeRate);

  /** Mirrors the server's conversion so the desk can see the result before submitting. */
  const amountUsd = isLbp
    ? received > 0 && rate > 0
      ? received / rate
      : null
    : received > 0
    ? received
    : null;

  const shortfall =
    selectedPackage && amountUsd !== null ? selectedPackage.price - amountUsd : null;
  const isShort = shortfall !== null && shortfall > 0.004;

  const isValid =
    !!selectedClient &&
    !!formData.packageId &&
    received > 0 &&
    (!isLbp || rate > 0);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid || !selectedClient) return;

    createMutation.mutate({
      clientId: selectedClient.id,
      packageId: formData.packageId as number,
      amountReceived: received,
      currency: formData.currency,
      exchangeRate: isLbp ? rate : undefined,
      paymentMethod: formData.paymentMethod,
      transactionReference: formData.transactionReference || undefined,
      notes: formData.notes || undefined,
    });
  };

  const errorMessage =
    (createMutation.error as { response?: { data?: { message?: string } } } | null)
      ?.response?.data?.message ?? 'Failed to record payment. Please try again.';

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={handleSubmit}>
        <DialogTitle>Take a payment</DialogTitle>
        <DialogContent>
          {createMutation.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {errorMessage}
            </Alert>
          )}

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
                    label="Member"
                    placeholder="Search by phone or name..."
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

            <Grid item xs={12}>
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
                    {pkg.name} — ${pkg.price.toFixed(2)} · {pkg.durationDays} days
                  </MenuItem>
                ))}
              </TextField>
            </Grid>

            <Grid item xs={12}>
              <ToggleButtonGroup
                exclusive
                fullWidth
                size="small"
                value={formData.currency}
                onChange={(_, value: CurrencyString | null) =>
                  value && setFormData({ ...formData, currency: value, amountReceived: '' })
                }
              >
                <ToggleButton value="Usd">Paid in USD</ToggleButton>
                <ToggleButton value="Lbp">Paid in LBP</ToggleButton>
              </ToggleButtonGroup>
            </Grid>

            {isLbp && (
              <Grid item xs={12} sm={6}>
                <TextField
                  fullWidth
                  required
                  label="Rate today"
                  type="number"
                  value={formData.exchangeRate}
                  onChange={(e) =>
                    setFormData({ ...formData, exchangeRate: e.target.value })
                  }
                  InputProps={{
                    endAdornment: <InputAdornment position="end">LBP / $</InputAdornment>,
                  }}
                  inputProps={{ min: '0.01', step: '0.01' }}
                  helperText="Stored with the payment and never recalculated"
                />
              </Grid>
            )}

            <Grid item xs={12} sm={isLbp ? 6 : 12}>
              <TextField
                fullWidth
                required
                label="Amount received"
                type="number"
                value={formData.amountReceived}
                onChange={(e) =>
                  setFormData({ ...formData, amountReceived: e.target.value })
                }
                InputProps={{
                  startAdornment: (
                    <InputAdornment position="start">{isLbp ? 'LBP' : '$'}</InputAdornment>
                  ),
                }}
                inputProps={{ min: '0.01', step: isLbp ? '1000' : '0.01' }}
              />
            </Grid>

            {selectedPackage && amountUsd !== null && (
              <Grid item xs={12}>
                <Box
                  sx={{
                    p: 1.5,
                    borderRadius: 1,
                    bgcolor: 'action.hover',
                    display: 'flex',
                    justifyContent: 'space-between',
                    gap: 2,
                  }}
                >
                  <Typography variant="body2" color="text.secondary">
                    {isLbp ? 'Converts to' : 'Counts as'}
                  </Typography>
                  <Typography variant="body2" fontWeight={600}>
                    ${amountUsd.toFixed(2)} of ${selectedPackage.price.toFixed(2)}
                  </Typography>
                </Box>
              </Grid>
            )}

            {isShort && (
              <Grid item xs={12}>
                <Alert severity="warning">
                  This is ${shortfall!.toFixed(2)} short of the package price. The payment
                  will be recorded and the member will show as owing the difference, but
                  their membership will <strong>not</strong> be extended.
                </Alert>
              </Grid>
            )}

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                select
                label="Method"
                value={formData.paymentMethod}
                onChange={(e) =>
                  setFormData({
                    ...formData,
                    paymentMethod: e.target.value as PaymentMethodString,
                  })
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
                label="Reference"
                placeholder="Receipt #, transfer ID (optional)"
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
            {createMutation.isPending ? 'Saving...' : 'Take payment'}
          </Button>
        </DialogActions>
      </form>
      <Snackbar
        open={showSuccess}
        autoHideDuration={3000}
        onClose={() => setShowSuccess(false)}
        message="Payment saved"
      />
    </Dialog>
  );
};
