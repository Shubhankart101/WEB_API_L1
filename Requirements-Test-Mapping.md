# Requirements to Test Mapping

This document maps SRS requirements to automated tests in the solution.

## Business Rules

| Requirement | Coverage |
|---|---|
| BR-1 Wallet affordability and blocking/unblocking on highest bid change | AuctionServiceTests.PlaceBidAsync_WithInsufficientWalletBalance_ReturnsError, AuctionServiceTests.PlaceBidAsync_WithValidBid_UpdatesAuctionAndWallet |
| BR-2 Ownership transfer on auction closure | AuctionServiceTests.CheckAuctionExpiriesAsync_WithWinningBid_TransfersOwnershipAndSettlesWallets |

## 4.1 Assets

| Requirement | Coverage |
|---|---|
| Create asset with valid input | AssetServiceTests.CreateAssetAsync_WithValidRequest_ReturnsSuccess |
| Title validation | AssetServiceTests.CreateAssetAsync_WithInvalidTitle_ReturnsValidationError |
| Update only in Draft status | AssetServiceTests.UpdateAssetAsync_AssetNotDraft_ReturnsValidationError |
| Asset not found on update | AssetServiceTests.UpdateAssetAsync_AssetNotFound_ReturnsNotFound |
| Delete only Draft/OpenToAuction | AssetServiceTests.DeleteAssetAsync_WhenAssetIsClosedForAuction_ReturnsValidationError, AssetServiceTests.DeleteAssetAsync_WhenAssetIsDraft_DeletesAssetSuccessfully |
| List assets for owner | Implemented in AssetService.GetAllAssetsByUserIdAsync. No direct unit test yet. |

## 4.2 Wallet

| Requirement | Coverage |
|---|---|
| Deposit positive amount and max limits | WalletServiceTests.DepositAsync_WithInvalidAmount_ReturnsValidationError, WalletServiceTests.DepositAsync_NewWallet_CreatesWallet, WalletServiceTests.DepositAsync_ExistingWallet_UpdatesAmount |
| Withdraw positive amount with sufficient available balance | WalletServiceTests.WithDrawalAsync_WithInvalidAmount_ReturnsValidationError, WalletServiceTests.WithDrawalAsync_WalletNotFound_ReturnsNotFound, WalletServiceTests.WithDrawalAsync_InsufficientBalance_ReturnsError, WalletServiceTests.WithDrawalAsync_SufficientBalance_Withdraws |
| Wallet dashboard with blocked amount and bid history | WalletServiceTests.GetWalletBalenceAsync_WalletNotFound_ReturnsNotFound, WalletServiceTests.GetWalletBalenceAsync_WalletFound_ReturnsBalance |

## 4.3 Dashboard

| Requirement | Coverage |
|---|---|
| Open auctions listing | Implemented in AuctionService.GetAllOpenAuctionsByUserIdAsync and API endpoint /api/Auction/open. No direct unit test yet. |

## 4.4 Auction

| Requirement | Coverage |
|---|---|
| Post auction validations (reserve/increment/expiry) | AuctionServiceTests.PostAuctionAsync_WithInvalidReservedPrice_ReturnsValidationError |
| Post auction with non-existing asset fails | AuctionServiceTests.PostAuctionAsync_AssetNotFound_ReturnsNotFound |
| Post auction success | AuctionServiceTests.PostAuctionAsync_WithValidRequest_ReturnsSuccess |
| Get auction by id | AuctionServiceTests.GetAuctionByIdAsync_AuctionNotFound_ReturnsNotFound, AuctionServiceTests.GetAuctionByIdAsync_AuctionFound_ReturnsAuctionResponse |
| Place bid flow | AuctionServiceTests.PlaceBidAsync_WithInsufficientWalletBalance_ReturnsError, AuctionServiceTests.PlaceBidAsync_WithValidBid_UpdatesAuctionAndWallet |
| Auction expiry without bids reopens asset | AuctionServiceTests.CheckAuctionExpiriesAsync_WithNoBids_ReopensAsset |

## Admin Use Cases (Section 2.2)

| Requirement | Coverage |
|---|---|
| Regenerate password | PortalUserServiceTests.AdminOperations_ShouldSupportResetDeleteAndAudit |
| Block/allow user access | PortalUserServiceTests.SetUserAccessAsync_BlockedUserLogin_ShouldFailWithUnauthorized |
| Verify user activity audit log | PortalUserServiceTests.AdminOperations_ShouldSupportResetDeleteAndAudit |
| Delete user | PortalUserServiceTests.AdminOperations_ShouldSupportResetDeleteAndAudit |

## Authentication

| Requirement | Coverage |
|---|---|
| JWT authentication token issuance | PortalUserServiceTests.LoginAsync_WithValidCredentials_ShouldReturnToken |
| JWT role claim for admins | PortalUserServiceTests.LoginAsync_AdminUserToken_ShouldContainAdminRoleClaim |
| Seeded default admin in API startup | TheAuctionHouse.API Program startup seeding logic (integration behavior) |
