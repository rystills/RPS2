<?php
// get roomCode
$roomCode = $_POST['room'] ?? '';
$username = $_POST['username'] ?? '';

// validate
if (empty($roomCode))
    exit;

// decrement user count
$usersFile = 'rooms/' . $roomCode . '_users.txt';
if (file_exists($usersFile))
{
    $roomFile = 'rooms/' . $roomCode . '.txt';
    $currentUsers = (int)file_get_contents($usersFile) - 1;
    
    if ($currentUsers > 0) {
        file_put_contents($usersFile, (string)($currentUsers));
        
        // send farewell message
        $userJoinedMessage = "$username has left the room [$currentUsers/4]\n";
        file_put_contents($roomFile, $userJoinedMessage, FILE_APPEND);
    }
    else
    {
        // room is empty; destroy it
        if (file_exists($roomFile))
            unlink($roomFile);
        unlink($usersFile);
    }
}
?>
