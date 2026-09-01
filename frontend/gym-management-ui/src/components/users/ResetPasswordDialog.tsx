import { useEffect, useState } from 'react';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogContentText,
  DialogTitle,
  TextField,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { userService } from '@services/userService';
import type { UserAccount } from '@app-types/index';
import { describeApiError } from '@lib/errors';

interface ResetPasswordDialogProps {
  open: boolean;
  onClose: () => void;
  user: UserAccount | null;
  onDone: (message: string) => void;
}

const MIN_PASSWORD_LENGTH = 12;

/**
 * An administrator setting someone else's password, for the case the gym will actually
 * hit: a person forgets theirs and asks at the desk.
 *
 * The old password is not asked for - the whole point is that nobody knows it.
 */
const ResetPasswordDialog = ({ open, onClose, user, onDone }: ResetPasswordDialogProps) => {
  const queryClient = useQueryClient();
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setPassword('');
    setConfirm('');
    setError(null);
  }, [open]);

  const reset = useMutation({
    mutationFn: () => userService.resetPassword(user!.id, { newPassword: password }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      onDone(`Password reset. ${user?.fullName} will need to sign in again.`);
      onClose();
    },
    onError: (err) => setError(describeApiError(err)),
  });

  const tooShort = password.length > 0 && password.length < MIN_PASSWORD_LENGTH;
  const mismatch = confirm.length > 0 && confirm !== password;
  const canSave = password.length >= MIN_PASSWORD_LENGTH && confirm === password;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Reset password</DialogTitle>

      <DialogContent>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <DialogContentText sx={{ mb: 2 }}>
          Set a new password for <strong>{user?.fullName}</strong> and tell them what it is.
          Anyone signed in as them will be signed out.
        </DialogContentText>

        <TextField
          label="New password"
          type="password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          fullWidth
          required
          autoFocus
          error={tooShort}
          helperText={tooShort ? `At least ${MIN_PASSWORD_LENGTH} characters` : ' '}
          sx={{ mb: 1 }}
        />

        <TextField
          label="Type it again"
          type="password"
          value={confirm}
          onChange={(e) => setConfirm(e.target.value)}
          fullWidth
          required
          error={mismatch}
          helperText={mismatch ? 'The two do not match' : ' '}
        />
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          variant="contained"
          color="warning"
          onClick={() => reset.mutate()}
          disabled={!canSave || reset.isPending}
        >
          Reset password
        </Button>
      </DialogActions>
    </Dialog>
  );
};

export default ResetPasswordDialog;
