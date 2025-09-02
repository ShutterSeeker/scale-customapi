/*
 Mod     | Programmer    | Date       | Modification Description
 --------------------------------------------------------------------
 001     | Blake Becker  | 08/27/2025 | Created.
*/

ALTER PROC [dbo].usp_UserAction (
    @action NVARCHAR(50)
    ,@internalID NVARCHAR(50)
    ,@changeValue NVARCHAR(50)
) AS

DECLARE @MessageCode NVARCHAR(50) = N'MSG_UNKNOWN01'
DECLARE @Message NVARCHAR(255) = N'Unknown action.'

IF @action = N'WavePriority'
BEGIN
    DECLARE @iLaunchNum NUMERIC (9,0)
    DECLARE @iPriority NUMERIC (3,0)


    SET @iLaunchNum = CONVERT(NUMERIC (9,0), @internalID)
    SET @iPriority = CONVERT(NUMERIC (3,0), @changeValue)

    UPDATE LAUNCH_STATISTICS SET
    USER_DEF7 = @iPriority
    WHERE INTERNAL_LAUNCH_NUM = @iLaunchNum

    EXEC usp_ChangePriority @iLaunchNum, NULL

    SET @MessageCode = N'MSG_CHANGEPRIORITY01'
    SET @Message = N'Change priority successful.'
END
ELSE IF @action = N'Other'
BEGIN
    SET @MessageCode = N'MSG_CHANGE01'
    SET @Message = N'Change successful.'
END

SELECT @MessageCode AS MessageCode, @Message AS Message