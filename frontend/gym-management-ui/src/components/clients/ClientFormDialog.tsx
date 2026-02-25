import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Grid,
  MenuItem,
  Alert,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { clientService } from '@services/clientService';
import { Client, GenderMap, GenderString } from '@types/index';

interface ClientFormDialogProps {
  open: boolean;
  onClose: () => void;
  client?: Client | null; // Optional client for edit mode
}

export const ClientFormDialog = ({ open, onClose, client }: ClientFormDialogProps) => {
  const queryClient = useQueryClient();
  const isEditMode = !!client;

  const [formData, setFormData] = useState({
    firstName: '',
    lastName: '',
    email: '',
    phoneNumber: '',
    dateOfBirth: '',
    gender: '' as 'Male' | 'Female' | 'Other' | '',
    address: '',
    emergencyContact: '',
    emergencyPhone: '',
    notes: '',
  });

  // Populate form when editing
  useEffect(() => {
    if (client && open) {
      setFormData({
        firstName: client.firstName || '',
        lastName: client.lastName || '',
        email: client.email || '',
        phoneNumber: client.phoneNumber || '',
        dateOfBirth: client.dateOfBirth ? client.dateOfBirth.split('T')[0] : '',
        gender: (client.gender || '') as 'Male' | 'Female' | 'Other' | '',
        address: client.address || '',
        emergencyContact: client.emergencyContact || '',
        emergencyPhone: client.emergencyPhone || '',
        notes: client.notes || '',
      });
    } else if (!open) {
      resetForm();
    }
  }, [client, open]);

  const createMutation = useMutation({
    mutationFn: (data: any) => clientService.createClient(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['clients'] });
      onClose();
      resetForm();
    },
  });

  const updateMutation = useMutation({
    mutationFn: (data: any) => clientService.updateClient(client!.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['clients'] });
      onClose();
      resetForm();
    },
  });

  const resetForm = () => {
    setFormData({
      firstName: '',
      lastName: '',
      email: '',
      phoneNumber: '',
      dateOfBirth: '',
      gender: '' as 'Male' | 'Female' | 'Other' | '',
      address: '',
      emergencyContact: '',
      emergencyPhone: '',
      notes: '',
    });
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    // Convert empty strings to null and gender string to enum number
    const cleanedData = {
      firstName: formData.firstName,
      lastName: formData.lastName,
      phoneNumber: formData.phoneNumber,
      email: formData.email || null,
      dateOfBirth: formData.dateOfBirth || null,
      gender: formData.gender ? GenderMap[formData.gender as GenderString] : null,
      address: formData.address || null,
      emergencyContact: formData.emergencyContact || null,
      emergencyPhone: formData.emergencyPhone || null,
      notes: formData.notes || null,
    };

    if (isEditMode) {
      updateMutation.mutate(cleanedData as any);
    } else {
      createMutation.mutate(cleanedData as any);
    }
  };

  const handleChange = (field: string) => (
    e: React.ChangeEvent<HTMLInputElement>
  ) => {
    setFormData({ ...formData, [field]: e.target.value });
  };

  const currentMutation = isEditMode ? updateMutation : createMutation;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <form onSubmit={handleSubmit}>
        <DialogTitle>{isEditMode ? 'Edit Client' : 'Add New Client'}</DialogTitle>
        <DialogContent>
          {currentMutation.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              Failed to {isEditMode ? 'update' : 'create'} client. Please try again.
            </Alert>
          )}

          <Grid container spacing={2} sx={{ mt: 1 }}>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                label="First Name"
                value={formData.firstName}
                onChange={handleChange('firstName')}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                label="Last Name"
                value={formData.lastName}
                onChange={handleChange('lastName')}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                label="Phone Number"
                value={formData.phoneNumber}
                onChange={handleChange('phoneNumber')}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                type="email"
                label="Email"
                value={formData.email}
                onChange={handleChange('email')}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                type="date"
                label="Date of Birth"
                value={formData.dateOfBirth}
                onChange={handleChange('dateOfBirth')}
                InputLabelProps={{ shrink: true }}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                select
                label="Gender"
                value={formData.gender}
                onChange={handleChange('gender')}
              >
                <MenuItem value="">Select Gender</MenuItem>
                <MenuItem value="Male">Male</MenuItem>
                <MenuItem value="Female">Female</MenuItem>
                <MenuItem value="Other">Other</MenuItem>
              </TextField>
            </Grid>
            <Grid item xs={12}>
              <TextField
                fullWidth
                label="Address"
                value={formData.address}
                onChange={handleChange('address')}
                multiline
                rows={2}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                label="Emergency Contact Name"
                value={formData.emergencyContact}
                onChange={handleChange('emergencyContact')}
              />
            </Grid>
            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                label="Emergency Contact Phone"
                value={formData.emergencyPhone}
                onChange={handleChange('emergencyPhone')}
              />
            </Grid>
            <Grid item xs={12}>
              <TextField
                fullWidth
                label="Notes"
                value={formData.notes}
                onChange={handleChange('notes')}
                multiline
                rows={3}
              />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancel</Button>
          <Button
            type="submit"
            variant="contained"
            disabled={currentMutation.isPending}
          >
            {currentMutation.isPending
              ? (isEditMode ? 'Updating...' : 'Creating...')
              : (isEditMode ? 'Update Client' : 'Create Client')
            }
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
};
