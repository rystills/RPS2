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
    $currentUsers = (int)file_get_contents($usersFile);
    if ($currentUsers > 0)
        file_put_contents($usersFile, (string)($currentUsers-1));
}
?>
