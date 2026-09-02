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
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { useMutation } from '@tanstack/react-query';
import { memberService } from '@services/memberService';
import { useAuthStore } from '@store/authStore';
import { describeApiError } from '@lib/errors';
import type { RegisterMemberRequest } from '@app-types/index';

const MIN_PASSWORD_LENGTH = 12;

/**
 * Where a member claims the membership the gym already made for them.
 *
 * The wording matters more than usual here. This is not "create an account" - it cannot
 * create a membership, and a member who believes it can will type their details, be told
 * they do not match, and conclude the gym has lost them. So the page says up front that
 * the gym has to have added them first.
 */
const RegisterPage = () => {
  const navigate = useNavigate();
  const { setTokens, setUser } = useAuthStore();

  const [form, setForm] = useState<RegisterMemberRequest>({
    phoneNumber: '',
    lastName: '',
    email: '',
    password: '',
    confirmPassword: '',
  });

  const [error, setError] = useState<string | null>(null);

  const registerMutation = useMutation({
    mutationFn: (data: RegisterMemberRequest) => memberService.register(data),
    onSuccess: (data) => {
      setTokens(data.accessToken, data.refreshToken);
      setUser(data.user);
      navigate('/member', { replace: true });
    },
    onError: (err) => setError(describeApiError(err)),
  });

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    // Checked here as well as on the server so the member is told before a round trip.
    if (form.password !== form.confirmPassword) {
      setError('The two passwords do not match.');
      return;
    }

    registerMutation.mutate(form);
  };

  return (
    <Container maxWidth="sm" sx={{ mt: { xs: 4, sm: 8 }, mb: 6 }}>
      <Paper elevation={3} sx={{ p: { xs: 3, sm: 4 } }}>
        <Typography variant="h4" align="center" gutterBottom>
          Set up your account
        </Typography>

        <Typography
          variant="body2"
          align="center"
          color="text.secondary"
          sx={{ mb: 3 }}
        >
          For members the gym has already signed up. Enter the phone number and surname the
          gym has for you, and choose how you would like to sign in.
        </Typography>

        {error && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {error}
          </Alert>
        )}

        <Box component="form" onSubmit={handleSubmit}>
          <TextField
            fullWidth
            label="Phone number"
            name="phoneNumber"
            value={form.phoneNumber}
            onChange={handleChange}
            margin="normal"
            required
            autoFocus
            autoComplete="tel"
            // The server strips spaces, dashes and the 961 country code before matching, so
            // there is no single right way to type it and the hint should not pretend there is.
            helperText="However you normally write it - 03 123 456 or +961 3 123 456 both work."
          />

          <TextField
            fullWidth
            label="Surname"
            name="lastName"
            value={form.lastName}
            onChange={handleChange}
            margin="normal"
            required
            autoComplete="family-name"
            helperText="As the gym has it."
          />

          <TextField
            fullWidth
            label="Email"
            name="email"
            type="email"
            value={form.email}
            onChange={handleChange}
            margin="normal"
            required
            autoComplete="email"
            helperText="This is what you will sign in with."
          />

          <TextField
            fullWidth
            label="Password"
            name="password"
            type="password"
            value={form.password}
            onChange={handleChange}
            margin="normal"
            required
            autoComplete="new-password"
            helperText={`At least ${MIN_PASSWORD_LENGTH} characters.`}
          />

          <TextField
            fullWidth
            label="Confirm password"
            name="confirmPassword"
            type="password"
            value={form.confirmPassword}
            onChange={handleChange}
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
            disabled={registerMutation.isPending}
          >
            {registerMutation.isPending ? 'Setting up...' : 'Create my account'}
          </Button>

          <Typography variant="body2" align="center" sx={{ mt: 2 }}>
            Already set up?{' '}
            <Link component={RouterLink} to="/login">
              Sign in
            </Link>
          </Typography>
        </Box>
      </Paper>
    </Container>
  );
};

export default RegisterPage;
