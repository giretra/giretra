//go:build windows

package main

import (
	"fmt"
	"os"
)

func startLauncherWatchdog(pid int) {
	p, err := os.FindProcess(pid)
	if err != nil {
		// Process doesn't exist — launcher already gone.
		fmt.Println("Launcher process exited, shutting down.")
		os.Exit(0)
		return
	}
	go func() {
		p.Wait()
		fmt.Println("Launcher process exited, shutting down.")
		os.Exit(0)
	}()
}
