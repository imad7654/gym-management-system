import { useEffect, useState } from 'react';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Grid,
  TextField,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { userService } from '@services/userService';
import type { UserAccount } from '@app-types/index';
import { describeApiError } from '@lib/errors';

interface UserFormDialogProps {
  open: boolean;
  onClose: () => void;
  /** The account being edited, or null to add a new one. */
  user: UserAccount | null;
  onSaved: (message: string) => void;
}

/** Matches Validation:MinPasswordLength on the server, which is the rule that is enforced. */
const MIN_PASSWORD_LENGTH = 12;

const UserFormDialog = ({ open, onClose, user, onSaved }: UserFormDialogProps) => {
  const queryClient = useQueryClient();
  const editing = user !== null;

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [password, setPassword] = useState('');
  const [role, setRole] = useState('Admin');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;

    // Names are split back out of the full name only for editing, where the server sent
    // one string. Everything after the first space is the surname.
    const [first, ...rest] = (user?.fullName ?? '').split(' ');

    setFirstName(user ? first : '');
    setLastName(user ? rest.join(' ') : '');
    setEmail(user?.email ?? '');
    setPhoneNumber(user?.phoneNumber ?? '');
    setPassword('');
    setRole(user?.roles?.includes('Staff') ? 'Staff' : 'Admin');
    setError(null);
  }, [open, user]);

  const save = useMutation({
    mutationFn: async () => {
      if (editing && user) {
        await userService.updateUser(user.id, {
          firstName: firstName.trim(),
          lastName: lastName.trim(),
          email: email.trim(),
          phoneNumber: phoneNumber.trim() || null,
          role,
        });
        return;
      }

      await userService.createUser({
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim() || null,
        password,
        role,
      });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      onSaved(editing ? 'Account updated' : 'Account created');
      onClose();
    },
    onError: (err) => setError(describeApiError(err)),
  });

  const passwordTooShort = !editing && password.length > 0 && password.length < MIN_PASSWORD_LENGTH;

  const canSave =
    firstName.trim().length > 0 &&
    lastName.trim().length > 0 &&
    email.trim().length > 0 &&
    (editing || password.length >= MIN_PASSWORD_LENGTH);

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{editing ? 'Edit account' : 'Add an account'}</DialogTitle>

      <DialogContent>
        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        {/*
          The warning is only true of an administrator, so it follows the choice rather
          than sitting above it. Telling someone that a reception account can see the
          money would be worse than saying nothing.
        */}
        {!editing && role === 'Admin' && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            An administrator can do everything you can — see the money, refund payments and
            remove members.
          </Alert>
        )}

        {!editing && (
          <Alert severity="info" sx={{ mb: 2 }}>
            There is no email in this system, so choose the password here and give it to
            them in person — they can change it once they are in.
          </Alert>
        )}

        <Grid container spacing={2} sx={{ mt: 0 }}>
          <Grid item xs={12}>
            <TextField
              select
              label="What they can do"
              value={role}
              onChange={(e) => setRole(e.target.value)}
              fullWidth
              SelectProps={{ native: true }}
              InputLabelProps={{ shrink: true }}
              helperText={
                role === 'Admin'
                  ? 'Everything, including the money and the audit trail.'
                  : 'Runs the desk. Cannot refund a payment, see revenue history, read the audit trail, change prices or manage accounts.'
              }
            >
              <option value="Admin">Administrator — the owner</option>
              <option value="Staff">Reception — the desk</option>
            </TextField>
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              label="First name"
              value={firstName}
              onChange={(e) => setFirstName(e.target.value)}
              fullWidth
              required
              autoFocus
            />
          </Grid>
          <Grid item xs={12} sm={6}>
            <TextField
              label="Last name"
              value={lastName}
              onChange={(e) => setLastName(e.target.value)}
              fullWidth
              required
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              label="Email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              fullWidth
              required
              helperText="This is what they sign in with"
            />
          </Grid>
          <Grid item xs={12}>
            <TextField
              label="Phone"
              value={phoneNumber}
              onChange={(e) => setPhoneNumber(e.target.value)}
              fullWidth
            />
          </Grid>

          {!editing && (
            <Grid item xs={12}>
              <TextField
                label="Password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                fullWidth
                required
                error={passwordTooShort}
                helperText={
                  passwordTooShort
                    ? `At least ${MIN_PASSWORD_LENGTH} characters`
                    : `At least ${MIN_PASSWORD_LENGTH} characters. They can change it after signing in.`
                }
              />
            </Grid>
          )}
        </Grid>
      </DialogContent>

      <DialogActions>
        <Button onClick={onClose}>Cancel</Button>
        <Button
          variant="contained"
          onClick={() => save.mutate()}
          disabled={!canSave || save.isPending}
        >
          {editing ? 'Save' : 'Create account'}
        </Button>
      </DialogActions>
    </Dialog>
  );
};

export default UserFormDialog;
