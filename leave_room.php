<?php
// get roomCode
$roomCode = $_POST['room'] ?? '';

// validate
if (empty($roomCode))
    exit;

// decrement user count
$roomFile = 'rooms/' . $roomCode . '_users.txt';
if (file_exists($roomFile))
{
    $currentUsers = (int)file_get_contents($roomFile);
    if ($currentUsers > 0)
        file_put_contents($roomFile, (string)($currentUsers-1));
}
?>
