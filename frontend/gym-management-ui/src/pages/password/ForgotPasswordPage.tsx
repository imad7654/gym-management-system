import { useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Container,
  Link,
  Paper,
  TextField,
  Typography,
} from '@mui/material';
import { Link as RouterLink } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { authService } from '@services/authService';
import { describeApiError } from '@lib/errors';

/**
 * "I forgot my password", for staff and members alike - both are the same kind of account.
 *
 * The confirmation deliberately does not say whether that address has an account. The
 * server answers identically either way, because this page is public and an answer that
 * differed would let anyone test which emails the gym holds. The wording has to match that
 * or it gives away what the API is careful not to.
 */
const ForgotPasswordPage = () => {
  const [email, setEmail] = useState('');
  const [sent, setSent] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const forgotMutation = useMutation({
    mutationFn: () => authService.forgotPassword(email),
    onSuccess: (message) => setSent(message),
    onError: (err) => setError(describeApiError(err)),
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    forgotMutation.mutate();
  };

  return (
    <Container maxWidth="sm" sx={{ mt: { xs: 4, sm: 8 } }}>
      <Paper elevation={3} sx={{ p: { xs: 3, sm: 4 } }}>
        <Typography variant="h4" align="center" gutterBottom>
          Forgot your password?
        </Typography>

        {sent ? (
          <>
            <Alert severity="success" sx={{ mb: 2 }}>
              {sent}
            </Alert>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              Check your inbox, and your spam folder. The link works once and expires in an
              hour.
            </Typography>
            <Button component={RouterLink} to="/login" fullWidth variant="outlined">
              Back to sign in
            </Button>
          </>
        ) : (
          <>
            <Typography
              variant="body2"
              align="center"
              color="text.secondary"
              sx={{ mb: 3 }}
            >
              Enter the email you sign in with and we will send you a link to choose a new
              password.
            </Typography>

            {error && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {error}
              </Alert>
            )}

            <Box component="form" onSubmit={handleSubmit}>
              <TextField
                fullWidth
                label="Email"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                margin="normal"
                required
                autoFocus
                autoComplete="email"
              />

              <Button
                type="submit"
                fullWidth
                variant="contained"
                size="large"
                sx={{ mt: 3 }}
                disabled={forgotMutation.isPending}
              >
                {forgotMutation.isPending ? 'Sending...' : 'Send me a link'}
              </Button>

              <Typography variant="body2" align="center" sx={{ mt: 2 }}>
                <Link component={RouterLink} to="/login">
                  Back to sign in
                </Link>
              </Typography>
            </Box>
          </>
        )}
      </Paper>
    </Container>
  );
};

export default ForgotPasswordPage;
