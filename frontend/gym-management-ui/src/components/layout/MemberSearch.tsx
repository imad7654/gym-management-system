import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Autocomplete,
  Box,
  CircularProgress,
  InputAdornment,
  TextField,
  Typography,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import { useQuery } from '@tanstack/react-query';
import { clientService } from '@services/clientService';
import { ClientListItem } from '@app-types/index';

/**
 * Find a member from anywhere in the app.
 *
 * The most common thing anyone does here is look someone up, and until now that meant
 * navigating to the member list first. Sitting in the top bar, it turns a three-step
 * journey into typing a name.
 *
 * The phone match is done by the server, which strips the separators the gym's records
 * actually use — so "70 123 456" finds a member saved as "70123456".
 */
export const MemberSearch = () => {
  const navigate = useNavigate();
  const [term, setTerm] = useState('');

  // Under a hundred members, so there is no paging to do: everyone who matches fits on
  // screen. The cap is a guard against a much larger gym, not a page size.
  const { data, isFetching } = useQuery({
    queryKey: ['clients', 'quick-search', term],
    queryFn: () => clientService.getClients({ page: 1, pageSize: 20, search: term }),
    enabled: term.trim().length > 0,
  });

  return (
    <Autocomplete
      size="small"
      options={data?.items ?? []}
      getOptionLabel={(option) => option.fullName}
      isOptionEqualToValue={(option, value) => option.id === value.id}
      filterOptions={(options) => options}
      inputValue={term}
      onInputChange={(_, value, reason) => {
        if (reason !== 'reset') setTerm(value);
      }}
      value={null}
      blurOnSelect
      clearOnBlur
      onChange={(_, option: ClientListItem | null) => {
        if (!option) return;
        setTerm('');
        navigate(`/admin/clients/${option.id}`);
      }}
      loading={isFetching}
      noOptionsText={term.trim() ? 'Nobody found' : 'Type a name or phone number'}
      sx={{
        width: { xs: '100%', sm: 260, md: 320 },
        '& .MuiOutlinedInput-root': { bgcolor: 'rgba(255,255,255,0.15)' },
        '& .MuiOutlinedInput-notchedOutline': { border: 'none' },
        '& input': { color: 'common.white' },
        '& input::placeholder': { color: 'rgba(255,255,255,0.8)', opacity: 1 },
      }}
      renderOption={(props, option) => (
        <Box component="li" {...props} key={option.id}>
          <Box>
            <Typography variant="body2">{option.fullName}</Typography>
            <Typography variant="caption" color="text.secondary">
              {option.phoneNumber} · {option.membershipStatus}
            </Typography>
          </Box>
        </Box>
      )}
      renderInput={(params) => (
        <TextField
          {...params}
          placeholder="Find a member"
          InputProps={{
            ...params.InputProps,
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon sx={{ color: 'rgba(255,255,255,0.85)' }} fontSize="small" />
              </InputAdornment>
            ),
            endAdornment: (
              <>
                {isFetching ? <CircularProgress size={16} sx={{ color: 'common.white' }} /> : null}
                {params.InputProps.endAdornment}
              </>
            ),
          }}
        />
      )}
    />
  );
};
