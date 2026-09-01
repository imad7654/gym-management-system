import { AxiosError } from 'axios';

/**
 * The message to show a person when a request fails.
 *
 * The server already writes these for a human to read - "This is the only administrator
 * who can still sign in", "An account already uses that email" - so the useful thing is to
 * pass them through rather than replace them with a generic apology.
 *
 * The fallback is only for the cases where there is nothing to pass through: the network
 * dropped, or the server fell over before it could say why.
 */
export const describeApiError = (
  error: unknown,
  fallback = 'Something went wrong. Please try again.'
): string => {
  const response = (error as AxiosError<{ message?: string; errors?: string[] }>)?.response;

  // Validation failures arrive as a list. Joining them beats showing only the first, since
  // a form usually has more than one thing wrong with it.
  const validationErrors = response?.data?.errors;
  if (validationErrors?.length) {
    return validationErrors.join(' ');
  }

  return response?.data?.message ?? fallback;
};
