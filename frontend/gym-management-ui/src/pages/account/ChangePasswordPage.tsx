import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Container,
  IconButton,
  InputAdornment,
  Paper,
  Snackbar,
  TextField,
  Typography,
} from '@mui/material';
import { Visibility, VisibilityOff } from '@mui/icons-material';
import { useMutation } from '@tanstack/react-query';
import { authService } from '@services/authService';
import { VALIDATION_CONFIG } from '@constants/config';

/**
 * Change your own password.
 *
 * This screen matters more than it looks: the first admin account is created with a
 * random password printed once to the API console, so without this there is no way to
 * ever set a password you can remember.
 */
const ChangePasswordPage = () => {
  const [showPasswords, setShowPasswords] = useState(false);
  const [showSuccess, setShowSuccess] = useState(false);
  const [form, setForm] = useState({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });

  const minLength = VALIDATION_CONFIG.MIN_PASSWORD_LENGTH;

  const changeMutation = useMutation({
    mutationFn: authService.changePassword,
    onSuccess: () => {
      setShowSuccess(true);
      setForm({ currentPassword: '', newPassword: '', confirmPassword: '' });
    },
  });

  const setField = (field: keyof typeof form) => (
    e: React.ChangeEvent<HTMLInputElement>
  ) => setForm((prev) => ({ ...prev, [field]: e.target.value }));

  // Mirrors the server's ChangePasswordRequestValidator so the desk gets told before
  // the round trip. The server still enforces all of it.
  const tooShort = form.newPassword.length > 0 && form.newPassword.length < minLength;
  const sameAsCurrent =
    form.newPassword.length > 0 && form.newPassword === form.currentPassword;
  const mismatch =
    form.confirmPassword.length > 0 && form.confirmPassword !== form.newPassword;

  const isValid =
    form.currentPassword.length > 0 &&
    form.newPassword.length >= minLength &&
    !sameAsCurrent &&
    form.confirmPassword === form.newPassword;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isValid) return;
    changeMutation.mutate(form);
  };

  const errorResponse = (
    changeMutation.error as {
      response?: { data?: { message?: string; errors?: string[] } };
    } | null
  )?.response?.data;

  const errorMessages = errorResponse?.errors?.length
    ? errorResponse.errors
    : [errorResponse?.message ?? 'Could not change your password. Please try again.'];

  const revealToggle = (
    <InputAdornment position="end">
      <IconButton
        onClick={() => setShowPasswords((v) => !v)}
        edge="end"
        aria-label={showPasswords ? 'Hide passwords' : 'Show passwords'}
      >
        {showPasswords ? <VisibilityOff /> : <Visibility />}
      </IconButton>
    </InputAdornment>
  );

  return (
    <Container maxWidth="sm" sx={{ mt: 4, mb: 4 }}>
      <Typography variant="h4" gutterBottom>
        Change password
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Pick something only you know. You stay signed in on this device after changing it.
      </Typography>

      <Paper sx={{ p: 3 }}>
        <form onSubmit={handleSubmit}>
          {changeMutation.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {errorMessages.map((message) => (
                <div key={message}>{message}</div>
              ))}
            </Alert>
          )}

          <TextField
            fullWidth
            required
            margin="normal"
            label="Current password"
            type={showPasswords ? 'text' : 'password'}
            autoComplete="current-password"
            value={form.currentPassword}
            onChange={setField('currentPassword')}
            InputProps={{ endAdornment: revealToggle }}
          />

          <TextField
            fullWidth
            required
            margin="normal"
            label="New password"
            type={showPasswords ? 'text' : 'password'}
            autoComplete="new-password"
            value={form.newPassword}
            onChange={setField('newPassword')}
            error={tooShort || sameAsCurrent}
            helperText={
              sameAsCurrent
                ? 'Your new password must be different from your current one'
                : `At least ${minLength} characters`
            }
            InputProps={{ endAdornment: revealToggle }}
          />

          <TextField
            fullWidth
            required
            margin="normal"
            label="Confirm new password"
            type={showPasswords ? 'text' : 'password'}
            autoComplete="new-password"
            value={form.confirmPassword}
            onChange={setField('confirmPassword')}
            error={mismatch}
            helperText={mismatch ? 'The two passwords do not match' : ' '}
            InputProps={{ endAdornment: revealToggle }}
          />

          <Box sx={{ display: 'flex', justifyContent: 'flex-end', mt: 2 }}>
            <Button
              type="submit"
              variant="contained"
              disabled={!isValid || changeMutation.isPending}
            >
              {changeMutation.isPending ? 'Changing...' : 'Change password'}
            </Button>
          </Box>
        </form>
      </Paper>

      <Snackbar
        open={showSuccess}
        autoHideDuration={4000}
        onClose={() => setShowSuccess(false)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity="success" onClose={() => setShowSuccess(false)}>
          Password changed.
        </Alert>
      </Snackbar>
    </Container>
  );
};

export default ChangePasswordPage;
