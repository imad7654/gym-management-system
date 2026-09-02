import { useState } from 'react';
import {
  Container,
  Paper,
  Typography,
  TextField,
  Button,
  Box,
  Alert,
  Link,
} from '@mui/material';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import type { AxiosError } from 'axios';
import { useMutation } from '@tanstack/react-query';
import { authService } from '@services/authService';
import { useAuthStore } from '@store/authStore';
import { LoginRequest } from '@app-types/index';

const LoginPage = () => {
  const navigate = useNavigate();
  const { setTokens, setUser, homePath } = useAuthStore();
  const [formData, setFormData] = useState<LoginRequest>({
    email: '',
    password: '',
  });

  const loginMutation = useMutation({
    mutationFn: (data: LoginRequest) => authService.login(data),
    onSuccess: (data) => {
      setTokens(data.accessToken, data.refreshToken);
      setUser(data.user);

      // Members and administrators do not share a home. Sending everyone to the admin
      // dashboard put members on a screen whose every request they are refused.
      navigate(homePath(), { replace: true });
    },
  });

  /**
   * What actually went wrong.
   *
   * This used to say "Invalid email or password" for every failure - including the API
   * being switched off, a CORS refusal or a 500. Somebody then hunts for a typo in a
   * password that was correct all along, which is exactly the wrong place to look. Only a
   * 401 is a credentials problem; everything else is the gym's own system being unreachable
   * and should say so.
   */
  const describeLoginFailure = (error: unknown): string => {
    const axiosError = error as AxiosError;

    if (axiosError?.response?.status === 401) {
      return 'Invalid email or password. Please try again.';
    }

    if (!axiosError?.response) {
      return 'Could not reach the gym system. Check it is running, then try again.';
    }

    return `The gym system returned an error (${axiosError.response.status}). Please try again.`;
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    loginMutation.mutate(formData);
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
  };

  return (
    <Container maxWidth="sm" sx={{ mt: 8 }}>
      <Paper elevation={3} sx={{ p: 4 }}>
        <Typography variant="h4" align="center" gutterBottom>
          Sign in
        </Typography>
        <Typography variant="body2" align="center" color="text.secondary" sx={{ mb: 3 }}>
          For gym staff and for members.
        </Typography>

        {loginMutation.isError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {describeLoginFailure(loginMutation.error)}
          </Alert>
        )}

        <Box component="form" onSubmit={handleSubmit}>
          <TextField
            fullWidth
            label="Email"
            name="email"
            type="email"
            value={formData.email}
            onChange={handleChange}
            margin="normal"
            required
            autoComplete="email"
            autoFocus
          />
          <TextField
            fullWidth
            label="Password"
            name="password"
            type="password"
            value={formData.password}
            onChange={handleChange}
            margin="normal"
            required
            autoComplete="current-password"
          />
          <Button
            type="submit"
            fullWidth
            variant="contained"
            size="large"
            sx={{ mt: 3 }}
            disabled={loginMutation.isPending}
          >
            {loginMutation.isPending ? 'Signing in...' : 'Sign In'}
          </Button>

          <Typography variant="body2" align="center" sx={{ mt: 2 }}>
            <Link component={RouterLink} to="/forgot-password">
              Forgot your password?
            </Link>
          </Typography>

          <Typography variant="body2" align="center" sx={{ mt: 1 }}>
            Are you a member without an account yet?{' '}
            <Link component={RouterLink} to="/register">
              Set one up
            </Link>
          </Typography>
        </Box>
      </Paper>
    </Container>
  );
};

export default LoginPage;
