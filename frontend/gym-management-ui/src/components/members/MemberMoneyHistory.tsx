import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import UndoIcon from '@mui/icons-material/Undo';
import { MemberPayment, MemberSummary } from '@app-types/index';
import { ReversePaymentDialog } from '@components/payments';

/**
 * What the member owes, and everything they have ever paid.
 *
 * Reversals are shown as corrections rather than as negative payments. A row reading
 * "-$30.00" beside a row reading "$30.00" invites the reader to think money went missing;
 * saying "Refunded" next to the payment it cancels is the same arithmetic told honestly.
 */
interface MemberMoneyHistoryProps {
  member: MemberSummary;
  onChanged: () => void;
  /** Removed members are read-only — nothing here should let their money be changed. */
  readOnly?: boolean;
}

const formatDateTime = (value: string) =>
  new Date(value).toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  });

const describeAmount = (payment: MemberPayment) => {
  const usd = `$${Math.abs(payment.amountUsd).toFixed(2)}`;

  // LBP payments record what actually changed hands and the rate it was taken at. Those
  // are never recalculated, so showing them is the only way the owner can check an old
  // payment against the notes that were in the drawer that day.
  if (payment.currency === 'Lbp') {
    return `${usd} (${Math.abs(payment.amountReceived).toLocaleString()} LBP)`;
  }

  return usd;
};

export const MemberMoneyHistory = ({
  member,
  onChanged,
  readOnly = false,
}: MemberMoneyHistoryProps) => {
  const [reversing, setReversing] = useState<MemberPayment | null>(null);

  return (
    <>
      {member.outstanding.length > 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          <Typography variant="body2" sx={{ fontWeight: 500, mb: 0.5 }}>
            Still owes ${member.totalOwed.toFixed(2)}
          </Typography>
          {member.outstanding.map((row) => (
            <Typography key={row.packageId} variant="body2">
              {row.packageName}: paid ${row.amountPaid.toFixed(2)} of $
              {row.packagePrice.toFixed(2)} — ${row.amountOwed.toFixed(2)} to go
            </Typography>
          ))}
          <Typography variant="caption" color="text.secondary" sx={{ mt: 0.5, display: 'block' }}>
            The membership extends automatically once the rest is paid.
          </Typography>
        </Alert>
      )}

      <Paper sx={{ p: { xs: 1.5, sm: 2 }, mb: 2 }}>
        <Typography variant="h6" sx={{ mb: 1.5 }}>
          Payments
        </Typography>

        {member.payments.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No payments recorded. Imported members arrive with the end date they already had
            and no payment history.
          </Typography>
        ) : (
          <Stack divider={<Divider flexItem />} spacing={0}>
            {member.payments.map((payment) => (
              <Box
                key={payment.id}
                sx={{
                  py: 1.25,
                  display: 'flex',
                  gap: 1,
                  alignItems: 'flex-start',
                  justifyContent: 'space-between',
                  flexWrap: 'wrap',
                }}
              >
                <Box sx={{ minWidth: 0 }}>
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>
                    {describeAmount(payment)}
                    {payment.isReversal && (
                      <Chip
                        label="Refunded"
                        size="small"
                        color="error"
                        variant="outlined"
                        sx={{ ml: 1 }}
                      />
                    )}
                  </Typography>
                  <Typography variant="caption" color="text.secondary" display="block">
                    {formatDateTime(payment.paidAt)} · {payment.paymentMethod}
                    {payment.packageName ? ` · ${payment.packageName}` : ''}
                  </Typography>
                  {payment.periodStartDate && payment.periodEndDate && (
                    <Typography variant="caption" color="success.main" display="block">
                      Bought {formatDateTime(payment.periodStartDate)} –{' '}
                      {formatDateTime(payment.periodEndDate)}
                    </Typography>
                  )}
                  {payment.notes && (
                    <Typography variant="caption" color="text.secondary" display="block">
                      {payment.notes}
                    </Typography>
                  )}
                </Box>

                {!readOnly && !payment.isReversal && (
                  <Button
                    size="small"
                    color="error"
                    startIcon={<UndoIcon />}
                    onClick={() => setReversing(payment)}
                  >
                    Refund
                  </Button>
                )}
              </Box>
            ))}
          </Stack>
        )}
      </Paper>

      {reversing && (
        <ReversePaymentDialog
          open={!!reversing}
          onClose={() => {
            setReversing(null);
            onChanged();
          }}
          payment={{
            id: reversing.id,
            clientName: member.fullName,
            amount: reversing.amountUsd,
          }}
        />
      )}
    </>
  );
};
