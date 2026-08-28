import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  InputAdornment,
  Paper,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { CurrencyExchange } from '@mui/icons-material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { exchangeRateService } from '@services/exchangeRateService';

/**
 * Today's LBP rate, set once each morning.
 *
 * The rate moves, so the desk should not be retyping it from memory on every payment —
 * that is how a wrong figure gets into the books and stays there, since a payment keeps
 * whatever rate it was taken at. Setting it here once means every LBP payment that day
 * starts from the same number, and the takings report converts against something the owner
 * actually chose.
 */
export const ExchangeRateCard = () => {
  const queryClient = useQueryClient();
  const [draft, setDraft] = useState('');

  const { data: current, isLoading } = useQuery({
    queryKey: ['exchange-rate', 'current'],
    queryFn: exchangeRateService.getCurrent,
  });

  // Seeded from the saved rate so correcting a typo means editing a number, not retyping
  // it. Only while the box is untouched, or it would fight the owner mid-edit.
  useEffect(() => {
    if (current && draft === '') setDraft(String(current.rate));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [current]);

  const saveMutation = useMutation({
    mutationFn: exchangeRateService.setToday,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['exchange-rate'] });
    },
  });

  const parsed = parseFloat(draft);
  const isValid = Number.isFinite(parsed) && parsed > 0;
  const changed = !current || parsed !== current.rate;

  return (
    <Paper sx={{ p: 3, mb: 3 }}>
      <Stack direction="row" spacing={1.5} alignItems="center" sx={{ mb: 0.5 }}>
        <CurrencyExchange color="primary" />
        <Typography variant="h6">Today&apos;s exchange rate</Typography>
      </Stack>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Set this each morning. The payment form fills it in for LBP payments, and reception
        can still override it for a single payment.
      </Typography>

      {!isLoading && !current && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          No rate has been set yet. Until one is, reception has to type the rate on every
          LBP payment.
        </Alert>
      )}

      {current?.isStale && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          This rate was set{' '}
          {current.daysOld === 1 ? 'yesterday' : `${current.daysOld} days ago`}. It is still
          being offered at the desk — check it against today&apos;s rate before taking LBP.
        </Alert>
      )}

      {saveMutation.isSuccess && !saveMutation.isPending && (
        <Alert severity="success" sx={{ mb: 2 }}>
          Today&apos;s rate is saved.
        </Alert>
      )}

      {saveMutation.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {(saveMutation.error as { response?: { data?: { message?: string } } } | null)
            ?.response?.data?.message ?? 'Could not save the rate. Please try again.'}
        </Alert>
      )}

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems={{ sm: 'flex-start' }}>
        <TextField
          label="LBP per 1 USD"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          type="number"
          inputProps={{ min: 0, step: 100 }}
          InputProps={{
            startAdornment: <InputAdornment position="start">LL</InputAdornment>,
          }}
          error={draft !== '' && !isValid}
          helperText={
            draft !== '' && !isValid ? 'Enter a rate greater than zero' : ' '
          }
          sx={{ maxWidth: 260 }}
        />
        <Box sx={{ pt: { sm: 1 } }}>
          <Button
            variant="contained"
            onClick={() => saveMutation.mutate(parsed)}
            disabled={!isValid || !changed || saveMutation.isPending}
          >
            {saveMutation.isPending ? 'Saving…' : "Save today's rate"}
          </Button>
        </Box>
      </Stack>
    </Paper>
  );
};
