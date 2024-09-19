<?php
// get roomCode
$roomCode = $_GET['room'] ?? '';

// validate
if (empty($roomCode))
{
    echo '';
    exit;
}

// return user count + messages from room file
$roomFile = 'rooms/' . $roomCode . '.txt';
if (file_exists($roomFile))
{
    $usersFile = 'rooms/' . $roomCode . '_users.txt';
    $currentUsers = file_get_contents($usersFile);
    
    $messages = file_get_contents($roomFile);
    echo "$currentUsers\n$messages";
}
else echo '';
?>
