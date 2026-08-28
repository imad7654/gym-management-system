import { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Container,
  Divider,
  Grid,
  Paper,
  Snackbar,
  TextField,
  Typography,
} from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { gymInfoService } from '@services/gymInfoService';
import { UpdateGymInfoRequest } from '@app-types/index';

const EMPTY_FORM: UpdateGymInfoRequest = {
  gymName: '',
  logoUrl: '',
  description: '',
  address: '',
  phoneNumber: '',
  email: '',
  facebookUrl: '',
  instagramUrl: '',
  twitterUrl: '',
  operatingHours: '',
  heroTitle: '',
  heroSubtitle: '',
  heroImageUrl: '',
  aboutTitle: '',
  aboutContent: '',
  metaTitle: '',
  metaDescription: '',
};

/**
 * The gym's own details: what the public homepage says, and how members reach you.
 *
 * There has been a GymInfo row in the database since the first migration, but nothing
 * could read or write it, so the homepage hardcoded its own copy instead. This is the
 * screen that makes that row mean something.
 */
const SettingsPage = () => {
  const queryClient = useQueryClient();
  const [showSuccess, setShowSuccess] = useState(false);
  const [form, setForm] = useState<UpdateGymInfoRequest>(EMPTY_FORM);

  const { data: gymInfo, isLoading, isError } = useQuery({
    queryKey: ['gym-info'],
    queryFn: gymInfoService.getGymInfo,
  });

  // Seed the form once the saved values arrive. Nulls become empty strings so the
  // fields stay controlled.
  useEffect(() => {
    if (!gymInfo) return;
    setForm({
      gymName: gymInfo.gymName ?? '',
      logoUrl: gymInfo.logoUrl ?? '',
      description: gymInfo.description ?? '',
      address: gymInfo.address ?? '',
      phoneNumber: gymInfo.phoneNumber ?? '',
      email: gymInfo.email ?? '',
      facebookUrl: gymInfo.facebookUrl ?? '',
      instagramUrl: gymInfo.instagramUrl ?? '',
      twitterUrl: gymInfo.twitterUrl ?? '',
      operatingHours: gymInfo.operatingHours ?? '',
      heroTitle: gymInfo.heroTitle ?? '',
      heroSubtitle: gymInfo.heroSubtitle ?? '',
      heroImageUrl: gymInfo.heroImageUrl ?? '',
      aboutTitle: gymInfo.aboutTitle ?? '',
      aboutContent: gymInfo.aboutContent ?? '',
      metaTitle: gymInfo.metaTitle ?? '',
      metaDescription: gymInfo.metaDescription ?? '',
    });
  }, [gymInfo]);

  const updateMutation = useMutation({
    mutationFn: gymInfoService.updateGymInfo,
    onSuccess: (updated) => {
      queryClient.setQueryData(['gym-info'], updated);
      setShowSuccess(true);
    },
  });

  const setField = (field: keyof UpdateGymInfoRequest) => (
    e: React.ChangeEvent<HTMLInputElement>
  ) => setForm((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.gymName.trim()) return;

    // Send blanks as undefined so a cleared field is stored as NULL, not an empty string.
    const payload = Object.fromEntries(
      Object.entries(form).map(([key, value]) => [
        key,
        typeof value === 'string' && value.trim() === '' ? undefined : value,
      ])
    ) as UpdateGymInfoRequest;

    updateMutation.mutate(payload);
  };

  const errorResponse = (
    updateMutation.error as {
      response?: { data?: { message?: string; errors?: string[] } };
    } | null
  )?.response?.data;

  const errorMessages = errorResponse?.errors?.length
    ? errorResponse.errors
    : [errorResponse?.message ?? 'Could not save your changes. Please try again.'];

  const field = (
    label: string,
    key: keyof UpdateGymInfoRequest,
    options: { multiline?: boolean; rows?: number; required?: boolean; help?: string } = {}
  ) => (
    <TextField
      fullWidth
      label={label}
      required={options.required}
      multiline={options.multiline}
      rows={options.rows}
      helperText={options.help}
      value={form[key] ?? ''}
      onChange={setField(key)}
    />
  );

  if (isLoading) {
    return (
      <Container maxWidth="md" sx={{ mt: 4, display: 'flex', justifyContent: 'center' }}>
        <CircularProgress />
      </Container>
    );
  }

  if (isError) {
    return (
      <Container maxWidth="md" sx={{ mt: 4 }}>
        <Alert severity="error">
          Could not load your gym&apos;s details. Check that the API is running, then
          reload this page.
        </Alert>
      </Container>
    );
  }

  return (
    <Container maxWidth="md" sx={{ mt: 4, mb: 4 }}>
      <Typography variant="h4" gutterBottom>
        Gym settings
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Your gym&apos;s name, contact details and the words on your public homepage.
      </Typography>

      <form onSubmit={handleSubmit}>
        {updateMutation.isError && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {errorMessages.map((message) => (
              <div key={message}>{message}</div>
            ))}
          </Alert>
        )}

        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" gutterBottom>
            The basics
          </Typography>
          <Divider sx={{ mb: 2 }} />
          <Grid container spacing={2}>
            <Grid item xs={12}>
              {field('Gym name', 'gymName', { required: true })}
            </Grid>
            <Grid item xs={12}>
              {field('Short description', 'description', { multiline: true, rows: 2 })}
            </Grid>
            <Grid item xs={12} sm={6}>
              {field('Phone number', 'phoneNumber')}
            </Grid>
            <Grid item xs={12} sm={6}>
              {field('Email', 'email')}
            </Grid>
            <Grid item xs={12}>
              {field('Address', 'address', { multiline: true, rows: 2 })}
            </Grid>
            <Grid item xs={12}>
              {field('Opening hours', 'operatingHours', {
                multiline: true,
                rows: 2,
                help: 'Free text, for example: Mon-Fri 6am-10pm, Sat-Sun 8am-6pm',
              })}
            </Grid>
            <Grid item xs={12}>
              {field('Logo URL', 'logoUrl')}
            </Grid>
          </Grid>
        </Paper>

        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" gutterBottom>
            Homepage
          </Typography>
          <Divider sx={{ mb: 2 }} />
          <Grid container spacing={2}>
            <Grid item xs={12}>
              {field('Hero title', 'heroTitle', {
                help: 'The big line at the top of your homepage',
              })}
            </Grid>
            <Grid item xs={12}>
              {field('Hero subtitle', 'heroSubtitle', { multiline: true, rows: 2 })}
            </Grid>
            <Grid item xs={12}>
              {field('Hero image URL', 'heroImageUrl')}
            </Grid>
            <Grid item xs={12}>
              {field('About heading', 'aboutTitle')}
            </Grid>
            <Grid item xs={12}>
              {field('About text', 'aboutContent', { multiline: true, rows: 4 })}
            </Grid>
          </Grid>
        </Paper>

        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" gutterBottom>
            Social links
          </Typography>
          <Divider sx={{ mb: 2 }} />
          <Grid container spacing={2}>
            <Grid item xs={12}>
              {field('Facebook URL', 'facebookUrl')}
            </Grid>
            <Grid item xs={12}>
              {field('Instagram URL', 'instagramUrl')}
            </Grid>
            <Grid item xs={12}>
              {field('X / Twitter URL', 'twitterUrl')}
            </Grid>
          </Grid>
        </Paper>

        <Paper sx={{ p: 3, mb: 3 }}>
          <Typography variant="h6" gutterBottom>
            Search engines
          </Typography>
          <Divider sx={{ mb: 2 }} />
          <Grid container spacing={2}>
            <Grid item xs={12}>
              {field('Page title', 'metaTitle', {
                help: 'What Google shows as the clickable heading',
              })}
            </Grid>
            <Grid item xs={12}>
              {field('Page description', 'metaDescription', { multiline: true, rows: 2 })}
            </Grid>
          </Grid>
        </Paper>

        <Box sx={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Button
            type="submit"
            variant="contained"
            disabled={!form.gymName.trim() || updateMutation.isPending}
          >
            {updateMutation.isPending ? 'Saving...' : 'Save changes'}
          </Button>
        </Box>
      </form>

      <Snackbar
        open={showSuccess}
        autoHideDuration={4000}
        onClose={() => setShowSuccess(false)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert severity="success" onClose={() => setShowSuccess(false)}>
          Saved. Your homepage is updated.
        </Alert>
      </Snackbar>
    </Container>
  );
};

export default SettingsPage;
