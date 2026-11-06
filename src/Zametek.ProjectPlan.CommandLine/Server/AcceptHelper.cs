using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Zametek.ProjectPlan.CommandLine
{
    // Content negotiation, as far as zpp serve's API needs it: which of the media types an endpoint offers a request's
    // Accept asks for.
    internal static class AcceptHelper
    {
        // The offered media type the request prefers: the one it gives the best quality, and the first of them - the
        // endpoint's own preference - if several get the same. The first of them when the request has no Accept; null when
        // none is acceptable. A range counts for what it matches most closely: a type by its name, then by its group, then
        // as */*.
        public static string? Choose(
            HttpRequest request,
            IReadOnlyList<string> offered)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(offered);

            IList<MediaTypeHeaderValue> accept = request.GetTypedHeaders().Accept;

            if (accept.Count == 0)
            {
                return offered[0];
            }

            string? chosen = null;
            double best = 0;

            foreach (string mediaType in offered)
            {
                double quality = GetQuality(mediaType, accept);

                if (quality > best)
                {
                    chosen = mediaType;
                    best = quality;
                }
            }

            return chosen;
        }

        // The quality the ranges give a media type: that of the one that matches it most closely, or 0 if none does.
        private static double GetQuality(
            string mediaType,
            IList<MediaTypeHeaderValue> accept)
        {
            var offered = new MediaTypeHeaderValue(mediaType);
            int closest = 0;
            double quality = 0;

            foreach (MediaTypeHeaderValue range in accept)
            {
                int closeness = GetCloseness(range, offered);

                if (closeness > closest)
                {
                    closest = closeness;
                    quality = range.Quality ?? 1;
                }
            }

            return quality;
        }

        // How closely a range matches a media type: 3 by its name, 2 by its group (application/*), 1 as */*, 0 not at all.
        private static int GetCloseness(
            MediaTypeHeaderValue range,
            MediaTypeHeaderValue offered)
        {
            if (range.MediaType.Equals(offered.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (range.MatchesAllSubTypes
                && !range.MatchesAllTypes
                && range.Type.Equals(offered.Type, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return range.MatchesAllTypes ? 1 : 0;
        }
    }
}
