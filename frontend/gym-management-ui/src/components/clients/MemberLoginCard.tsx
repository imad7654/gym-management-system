import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  Paper,
  Snackbar,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { memberService } from '@services/memberService';
import { describeApiError } from '@lib/errors';

const MIN_PASSWORD_LENGTH = 12;

interface MemberLoginCardProps {
  clientId: number;
  memberName: string;
}

/**
 * Whether this member can sign in, and the owner's one lever over it.
 *
 * The gym cannot create a member's account for them - sign-up is the member matching their
 * own phone number and surname - so this card mostly answers a question reception gets
 * asked at the desk: "have I got an account?". The reset is here because there is no email
 * anywhere in this system, which makes the owner the only password recovery a member has.
 */
const MemberLoginCard = ({ clientId, memberName }: MemberLoginCardProps) => {
  const queryClient = useQueryClient();

  const [resetOpen, setResetOpen] = useState(false);
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);

  const accountQuery = useQuery({
    queryKey: ['member-account', clientId],
    queryFn: () => memberService.getAccount(clientId),
  });

  useEffect(() => {
    if (!resetOpen) return;
    setPassword('');
    setConfirm('');
    setError(null);
  }, [resetOpen]);

  const resetMutation = useMutation({
    mutationFn: () =>
      memberService.resetPassword(clientId, {
        newPassword: password,
        confirmPassword: confirm,
      }),
    onSuccess: () => {
      setResetOpen(false);

      // The password is never stored anywhere readable, so the only chance to pass it on is
      // now, while the person who typed it is still looking at the screen.
      setDone(`Password reset. Tell ${memberName} the new password - it is not saved anywhere.`);
      queryClient.invalidateQueries({ queryKey: ['member-account', clientId] });
    },
    onError: (err) => setError(describeApiError(err)),
  });

  const handleReset = () => {
    setError(null);

    if (password.length < MIN_PASSWORD_LENGTH) {
      setError(`The password must be at least ${MIN_PASSWORD_LENGTH} characters.`);
      return;
    }

    if (password !== confirm) {
      setError('The two passwords do not match.');
      return;
    }

    resetMutation.mutate();
  };

  const account = accountQuery.data;

  return (
    <>
      <Paper sx={{ p: 2, mt: 2 }}>
        <Typography variant="h6" sx={{ mb: 1 }}>
          Member login
        </Typography>

        {accountQuery.isLoading && (
          <Typography variant="body2" color="text.secondary">
            Checking...
          </Typography>
        )}

        {account && !account.hasAccount && (
          <Box>
            <Typography variant="body2" color="text.secondary">
              {memberName} has not set up an account yet.
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Members set their own up, by matching their phone number and surname. There is
              nothing to do here until they have.
            </Typography>
          </Box>
        )}

        {account?.hasAccount && (
          <Stack spacing={1}>
            <Stack direction="row" justifyContent="space-between" alignItems="center">
              <Box>
                <Typography variant="body2">{account.email}</Typography>
                <Typography variant="caption" color="text.secondary">
                  Set up{' '}
                  {account.createdAt
                    ? new Date(account.createdAt).toLocaleDateString('en-GB')
                    : 'at some point'}
                </Typography>
              </Box>

              <Button size="small" variant="outlined" onClick={() => setResetOpen(true)}>
                Reset password
              </Button>
            </Stack>

            {!account.isActive && (
              <Alert severity="warning">
                This login is switched off, so {memberName} cannot sign in.
              </Alert>
            )}
          </Stack>
        )}
      </Paper>

      <Dialog open={resetOpen} onClose={() => setResetOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>Reset {memberName}&apos;s password</DialogTitle>
        <DialogContent>
          <DialogContentText sx={{ mb: 2 }}>
            Choose a new password and tell it to them. Every device they are signed in on
            will be signed out.
          </DialogContentText>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <TextField
            autoFocus
            fullWidth
            type="password"
            label="New password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            margin="dense"
            helperText={`At least ${MIN_PASSWORD_LENGTH} characters.`}
          />

          <TextField
            fullWidth
            type="password"
            label="Confirm password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            margin="dense"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setResetOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={handleReset}
            disabled={resetMutation.isPending}
          >
            {resetMutation.isPending ? 'Resetting...' : 'Reset password'}
          </Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={done != null}
        autoHideDuration={10000}
        onClose={() => setDone(null)}
        message={done ?? ''}
      />
    </>
  );
};

export default MemberLoginCard;
