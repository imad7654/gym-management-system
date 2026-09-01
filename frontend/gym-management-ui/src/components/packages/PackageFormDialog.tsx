import { useState, useEffect } from 'react';
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  TextField,
  Grid,
  Alert,
  Switch,
  FormControlLabel,
  InputAdornment,
  Snackbar,
} from '@mui/material';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { packageService } from '@services/packageService';
import type {
  CreatePackageRequest,
  Package,
  UpdatePackageRequest,
} from '../../types';

interface PackageFormDialogProps {
  open: boolean;
  onClose: () => void;
  package?: Package | null; // Optional package for edit mode
}

export const PackageFormDialog = ({ open, onClose, package: pkg }: PackageFormDialogProps) => {
  const queryClient = useQueryClient();
  const isEditMode = !!pkg;
  const [showSuccess, setShowSuccess] = useState(false);

  const [formData, setFormData] = useState({
    name: '',
    description: '',
    price: '',
    durationDays: '',
    isActive: true,
    displayOrder: '',
  });

  // Populate form when editing
  useEffect(() => {
    if (pkg && open) {
      setFormData({
        name: pkg.name || '',
        description: pkg.description || '',
        price: pkg.price?.toString() || '',
        durationDays: pkg.durationDays?.toString() || '',
        isActive: pkg.isActive ?? true,
        displayOrder: pkg.displayOrder?.toString() || '0',
      });
    } else if (!open) {
      resetForm();
    }
  }, [pkg, open]);

  const createMutation = useMutation({
    mutationFn: (data: CreatePackageRequest) => packageService.createPackage(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['packages'] });
      setShowSuccess(true);
      setTimeout(() => {
        onClose();
        resetForm();
      }, 1000);
    },
  });

  const updateMutation = useMutation({
    mutationFn: (data: UpdatePackageRequest) => packageService.updatePackage(pkg!.id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['packages'] });
      setShowSuccess(true);
      setTimeout(() => {
        onClose();
        resetForm();
      }, 1000);
    },
  });

  const resetForm = () => {
    setFormData({
      name: '',
      description: '',
      price: '',
      durationDays: '',
      isActive: true,
      displayOrder: '',
    });
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    // Validate required fields
    if (!formData.name || !formData.price || !formData.durationDays) {
      return;
    }

    const cleanedData = {
      name: formData.name,
      description: formData.description || undefined,
      price: parseFloat(formData.price),
      durationDays: parseInt(formData.durationDays, 10),
      isActive: formData.isActive,
      displayOrder: formData.displayOrder ? parseInt(formData.displayOrder, 10) : 0,
    };

    if (isEditMode) {
      updateMutation.mutate(cleanedData);
    } else {
      createMutation.mutate(cleanedData);
    }
  };

  const handleChange = (field: string) => (
    e: React.ChangeEvent<HTMLInputElement>
  ) => {
    const value = field === 'isActive' ? e.target.checked : e.target.value;
    setFormData({ ...formData, [field]: value });
  };

  const currentMutation = isEditMode ? updateMutation : createMutation;

  return (
    <Dialog open={open} onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={handleSubmit}>
        <DialogTitle>{isEditMode ? 'Edit Package' : 'Add New Package'}</DialogTitle>
        <DialogContent>
          {currentMutation.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              Failed to {isEditMode ? 'update' : 'create'} package. Please try again.
            </Alert>
          )}

          <Grid container spacing={2} sx={{ mt: 1 }}>
            <Grid item xs={12}>
              <TextField
                fullWidth
                required
                label="Package Name"
                value={formData.name}
                onChange={handleChange('name')}
                inputProps={{ maxLength: 100 }}
                helperText={`${formData.name.length}/100 characters`}
              />
            </Grid>

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                label="Price"
                type="number"
                value={formData.price}
                onChange={handleChange('price')}
                InputProps={{
                  startAdornment: <InputAdornment position="start">$</InputAdornment>,
                }}
                inputProps={{ min: '0.01', step: '0.01' }}
              />
            </Grid>

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                required
                label="Duration (Days)"
                type="number"
                value={formData.durationDays}
                onChange={handleChange('durationDays')}
                inputProps={{ min: '1', max: '1825', step: '1' }}
                helperText={formData.durationDays ? `${Math.floor(parseInt(formData.durationDays) / 30)} months` : ''}
              />
            </Grid>

            <Grid item xs={12}>
              <TextField
                fullWidth
                label="Description"
                value={formData.description}
                onChange={handleChange('description')}
                multiline
                rows={3}
                placeholder="Brief description of what this package includes..."
              />
            </Grid>

            <Grid item xs={12} sm={6}>
              <TextField
                fullWidth
                label="Display Order"
                type="number"
                value={formData.displayOrder}
                onChange={handleChange('displayOrder')}
                inputProps={{ min: '0', step: '1' }}
                helperText="Lower numbers appear first"
              />
            </Grid>

            <Grid item xs={12} sm={6}>
              <FormControlLabel
                control={
                  <Switch
                    checked={formData.isActive}
                    onChange={handleChange('isActive')}
                    color="success"
                  />
                }
                label="Active Package"
              />
            </Grid>
          </Grid>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>Cancel</Button>
          <Button
            type="submit"
            variant="contained"
            disabled={currentMutation.isPending || !formData.name || !formData.price || !formData.durationDays}
          >
            {currentMutation.isPending
              ? (isEditMode ? 'Updating...' : 'Creating...')
              : (isEditMode ? 'Update Package' : 'Create Package')
            }
          </Button>
        </DialogActions>
      </form>
      <Snackbar
        open={showSuccess}
        autoHideDuration={3000}
        onClose={() => setShowSuccess(false)}
        message={`Package ${isEditMode ? 'updated' : 'created'} successfully!`}
      />
    </Dialog>
  );
};
