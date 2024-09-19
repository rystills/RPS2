<?php
// get roomCode
$roomCode = $_POST['room'] ?? '';

// validate
if (empty($roomCode))
    exit;

// decrement user count
$usersFile = 'rooms/' . $roomCode . '_users.txt';
if (file_exists($usersFile))
{
    $currentUsers = (int)file_get_contents($usersFile) - 1;
    if ($currentUsers >= 0) {
        file_put_contents($usersFile, (string)($currentUsers));
        
        // send farewell message
        $roomFile = 'rooms/' . $roomCode . '.txt';
        $userJoinedMessage = "A user has left the room [$currentUsers/4]\n";
        file_put_contents($roomFile, $userJoinedMessage, FILE_APPEND);
    }
}
?>
