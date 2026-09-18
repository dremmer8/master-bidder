using MasterBidder.Campaign;

namespace MasterBidder.Core
{
    public static class LicenseRules
    {
        public static bool Owns(GameState state, string licenseId) =>
            state?.OwnedLicenses != null
            && !string.IsNullOrEmpty(licenseId)
            && state.OwnedLicenses.Contains(licenseId);

        public static bool OwnsRegular(GameState state) =>
            Owns(state, CampaignConfig.LicenseRegularId);

        public static bool OwnsElite(GameState state) =>
            Owns(state, CampaignConfig.LicenseEliteId);

        public static int ExamFee(string licenseId)
        {
            if (licenseId == CampaignConfig.LicenseEliteId)
                return CampaignConfig.LicenseExamFeeElite;
            return CampaignConfig.LicenseExamFeeRegular;
        }

        /// <summary>
        /// Without Regular → max Local. Without Elite → max Regular.
        /// </summary>
        public static VenueTier ClampVenue(VenueTier raw, GameState state)
        {
            VenueTier max = VenueTier.Local;
            if (OwnsRegular(state)) max = VenueTier.Regular;
            if (OwnsElite(state) && OwnsRegular(state)) max = VenueTier.Elite;

            if ((int)raw <= (int)max) return raw;
            return max;
        }

        public static bool CanAttemptExam(GameState state, string licenseId, int discoveredCount)
        {
            if (state == null || string.IsNullOrEmpty(licenseId)) return false;
            if (Owns(state, licenseId)) return false;
            if (licenseId == CampaignConfig.LicenseEliteId && !OwnsRegular(state))
                return false;
            if (discoveredCount <= 0) return false;
            return state.Capital >= ExamFee(licenseId);
        }

        public static void Grant(GameState state, string licenseId)
        {
            if (state == null || string.IsNullOrEmpty(licenseId)) return;
            if (state.OwnedLicenses == null)
                state.OwnedLicenses = new System.Collections.Generic.HashSet<string>();
            state.OwnedLicenses.Add(licenseId);
        }
    }
}
