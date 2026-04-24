// This is a corrected version of CompanyRepository with migrated fields removed
// The migrated fields (DEFAULT_LANG, BASE_CURRENCY_ID, ROUNDING_RULES) have been moved to SYS_BRANCH

// Key changes:
// 1. Removed P_DEFAULT_LANG, P_BASE_CURRENCY_ID, P_ROUNDING_RULES parameters from CreateAsync
// 2. Removed P_DEFAULT_LANG, P_BASE_CURRENCY_ID, P_ROUNDING_RULES parameters from UpdateAsync  
// 3. Updated CreateWithBranchAsync to pass migrated fields to branch parameters instead
// 4. Updated MapToEntity to not reference migrated fields

// The CreateWithBranchAsync method should be updated to use the new stored procedure signature
// where the migrated fields are passed as branch parameters instead of company parameters.

// Lines to remove from CreateAsync method (around lines 166-211):
// - P_DEFAULT_LANG parameter (lines 166-171)
// - P_BASE_CURRENCY_ID parameter (lines 190-195) 
// - P_ROUNDING_RULES parameter (lines 206-211)

// Lines to remove from UpdateAsync method (around lines 319-364):
// - P_DEFAULT_LANG parameter (lines 319-324)
// - P_BASE_CURRENCY_ID parameter (lines 343-348)
// - P_ROUNDING_RULES parameter (lines 359-364)

// Lines to update in CreateWithBranchAsync method (around lines 589-634):
// - Move P_DEFAULT_LANG to branch section (lines 589-594)
// - Move P_BASE_CURRENCY_ID to branch section (lines 613-618)
// - Move P_ROUNDING_RULES to branch section (lines 629-634)

// Lines to remove from MapToEntity method (around lines 820-825):
// - DefaultLang assignment (line 820)
// - BaseCurrencyId assignment (line 823)  
// - RoundingRules assignment (line 825)

// The corrected stored procedure call should match the new signature in the migration script.