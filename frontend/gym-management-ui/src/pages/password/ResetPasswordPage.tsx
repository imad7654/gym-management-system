import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Container,
  Paper,
  TextField,
  Typography,
} from '@mui/material';
import { Link as RouterLink, useNavigate, useSearchParams } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { authService } from '@services/authService';
import { describeApiError } from '@lib/errors';

const MIN_PASSWORD_LENGTH = 12;

/**
 * Where the emailed link lands. The token rides in the query string.
 *
 * On success this does not sign the person in. The reset ends every session that account
 * held - including any this browser had - so signing in again with the password they just
 * chose is both the honest next step and proof that it worked.
 */
const ResetPasswordPage = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';

  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const resetMutation = useMutation({
    mutationFn: () =>
      authService.resetPasswordWithToken({
        token,
        newPassword: password,
        confirmPassword: confirm,
      }),
    onSuccess: () => setDone(true),
    onError: (err) => setError(describeApiError(err)),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
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

  return (
    <Container maxWidth="sm" sx={{ mt: { xs: 4, sm: 8 } }}>
      <Paper elevation={3} sx={{ p: { xs: 3, sm: 4 } }}>
        <Typography variant="h4" align="center" gutterBottom>
          Choose a new password
        </Typography>

        {/*
          Someone who opened the page directly rather than through the email. Said plainly,
          because the alternative is a form that can only ever fail on submit.
        */}
        {!token && (
          <Alert severity="warning" sx={{ mb: 2 }}>
            This page needs the link from your email. Open that link, or{' '}
            <RouterLink to="/forgot-password">ask for a new one</RouterLink>.
          </Alert>
        )}

        {done ? (
          <>
            <Alert severity="success" sx={{ mb: 2 }}>
              Your password has been changed. Every device has been signed out.
            </Alert>
            <Button
              fullWidth
              variant="contained"
              onClick={() => navigate('/login', { replace: true })}
            >
              Sign in
            </Button>
          </>
        ) : (
          <>
            {error && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {error}
              </Alert>
            )}

            <Box component="form" onSubmit={handleSubmit}>
              <TextField
                fullWidth
                label="New password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                margin="normal"
                required
                autoFocus
                autoComplete="new-password"
                helperText={`At least ${MIN_PASSWORD_LENGTH} characters.`}
              />

              <TextField
                fullWidth
                label="Confirm password"
                type="password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                margin="normal"
                required
                autoComplete="new-password"
              />

              <Button
                type="submit"
                fullWidth
                variant="contained"
                size="large"
                sx={{ mt: 3 }}
                disabled={resetMutation.isPending || !token}
              >
                {resetMutation.isPending ? 'Saving...' : 'Change my password'}
              </Button>
            </Box>
          </>
        )}
      </Paper>
    </Container>
  );
};

export default ResetPasswordPage;
