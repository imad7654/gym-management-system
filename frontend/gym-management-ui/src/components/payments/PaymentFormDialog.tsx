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
import { exchangeRateService } from '@services/exchangeRateService';
import {
  ClientListItem,
  CurrencyString,
  PaymentMethodMap,
  PaymentMethodString,
} from '@app-types/index';

interface PaymentFormDialogProps {
  open: boolean;
  onClose: () => void;
  /**
   * Opened from a member's own page, so the member is already decided and the search box is
   * replaced by their name. This is what removes the second search from a renewal.
   */
  lockedClient?: { id: number; fullName: string };
  /**
   * Preselected package — the one they still owe on, or failing that the one they last
   * bought. Renewing the same package is the common case, and the other packages stay
   * visible so switching is one tap rather than a different screen.
   */
  defaultPackageId?: number;
}

/**
 * The desk payment form.
 *
 * Reception enters only what it can actually observe: who is paying, for what, how, and
 * how much changed hands. The price, the membership period and the USD conversion are all
 * worked out by the server from the package. The figures shown below the form are a
 * preview of that calculation, never the source of it.
 */
export const PaymentFormDialog = ({
  open,
  onClose,
  lockedClient,
  defaultPackageId,
}: PaymentFormDialogProps) => {
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

  const { data: todaysRate } = useQuery({
    queryKey: ['exchange-rate', 'current'],
    queryFn: exchangeRateService.getCurrent,
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

  // Opened from a member's page: the member and usually the package are already known, so
  // the form starts where reception would otherwise have had to navigate to.
  useEffect(() => {
    if (!open || !lockedClient) return;

    setSelectedClient({
      id: lockedClient.id,
      fullName: lockedClient.fullName,
      phoneNumber: '',
      membershipStatus: 'Pending',
      paymentStatus: 'Pending',
      isActive: true,
    });

    if (defaultPackageId) {
      setFormData((prev) => ({ ...prev, packageId: defaultPackageId }));
    }
  }, [open, lockedClient, defaultPackageId]);

  // Fill in the rate the owner set this morning, so reception is not retyping it from
  // memory on every LBP payment. Only when the box is still empty: a rate typed for this
  // one payment must survive, since the payment keeps whatever rate it was taken at.
  useEffect(() => {
    if (!open || !isLbp || !todaysRate) return;
    setFormData((prev) =>
      prev.exchangeRate === ''
        ? { ...prev, exchangeRate: String(todaysRate.rate) }
        : prev
    );
  }, [open, isLbp, todaysRate]);

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

  /**
   * Says where the rate came from. Reception needs to know whether the number in the box
   * is this morning's or a leftover, because it is stored with the payment for good.
   */
  const rateHelperText = !todaysRate
    ? 'No rate set in Settings — type today\'s rate'
    : todaysRate.isStale
      ? `Last set ${
          todaysRate.daysOld === 1 ? 'yesterday' : `${todaysRate.daysOld} days ago`
        } — check it before taking LBP`
      : "Today's rate, from Settings. Change it for this one payment if needed";

  /** Mirrors the server's conversion so the desk can see the result before submitting. */
  const amountUsd = isLbp
    ? received > 0 && rate > 0
      ? received / rate
      : null
    : received > 0
    ? received
    : null;

  /**
   * What this member has already put toward the selected package without getting anything
   * for it yet — the server's own definition, not a second calculation.
   *
   * Without this the form compared the amount being typed against the full package price
   * on its own. So a member who had already paid $20 of $30 and came back with the last
   * $10 was warned their membership would not be extended — when the server was about to
   * credit the $20, reach the price, and extend it. The screen said the opposite of what
   * happened.
   */
  const { data: outstanding } = useQuery({
    queryKey: ['clients', selectedClient?.id, 'outstanding'],
    queryFn: () => clientService.getOutstanding(selectedClient!.id),
    enabled: open && !!selectedClient,
  });

  const creditOnPackage =
    outstanding?.find((row) => row.packageId === formData.packageId)?.amountPaid ?? 0;

  const totalTowardPackage = amountUsd !== null ? amountUsd + creditOnPackage : null;

  const shortfall =
    selectedPackage && totalTowardPackage !== null
      ? selectedPackage.price - totalTowardPackage
      : null;

  // A fraction of a cent is rounding, not a debt.
  const isShort = shortfall !== null && shortfall > 0.004;

  /** True when earlier part payments are what tip this one over the line. */
  const completesWithCredit =
    creditOnPackage > 0 && shortfall !== null && shortfall <= 0.004;

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
              {lockedClient ? (
                <TextField
                  fullWidth
                  label="Member"
                  value={lockedClient.fullName}
                  InputProps={{ readOnly: true }}
                  helperText="Taking a payment from this member's page"
                />
              ) : (
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
              )}
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
                {/*
                  Always renders at least one child. The packages query only starts when the
                  dialog opens, so on the first open of a session this list is briefly
                  undefined - and a MUI select with no children errors out, taking the
                  package dropdown with it. Reception would hit that every time they took
                  the first payment after opening the app.
                */}
                {packages?.length ? (
                  packages.map((pkg) => (
                    <MenuItem key={pkg.id} value={pkg.id}>
                      {pkg.name} — ${pkg.price.toFixed(2)} · {pkg.durationDays} days
                    </MenuItem>
                  ))
                ) : (
                  <MenuItem value="" disabled>
                    Loading packages…
                  </MenuItem>
                )}
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
                  helperText={rateHelperText}
                  // A stale rate is still offered, but the desk is told, because the
                  // payment keeps whatever rate it was taken at.
                  color={todaysRate?.isStale ? 'warning' : undefined}
                  focused={todaysRate?.isStale ? true : undefined}
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
                    ${amountUsd.toFixed(2)}
                    {creditOnPackage > 0
                      ? ` + $${creditOnPackage.toFixed(2)} already paid`
                      : ''}{' '}
                    of ${selectedPackage.price.toFixed(2)}
                  </Typography>
                </Box>
              </Grid>
            )}

            {completesWithCredit && (
              <Grid item xs={12}>
                <Alert severity="success">
                  This finishes the {selectedPackage?.name} package — ${creditOnPackage.toFixed(2)}
                  {' '}was already down. The membership will be extended.
                </Alert>
              </Grid>
            )}

            {isShort && (
              <Grid item xs={12}>
                <Alert severity="warning">
                  {creditOnPackage > 0
                    ? `With the $${creditOnPackage.toFixed(2)} already paid, this is still $${shortfall!.toFixed(
                        2
                      )} short of the package price.`
                    : `This is $${shortfall!.toFixed(2)} short of the package price.`}{' '}
                  The payment will be recorded and the member will show as owing the
                  difference, but their membership will <strong>not</strong> be extended.
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
                    {method === 'Whish' ? 'Whish Money' : method}
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
